param(
    [string]$ApiBase = "http://localhost:8080",
    [string]$AdminEmail = "admin@saekg.local",
    [string]$AdminPassword = "AdminPass1"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$results = New-Object System.Collections.Generic.List[object]
$suffix = [guid]::NewGuid().ToString("N").Substring(0, 8)
$tmp = Join-Path $env:TEMP ("saekg-e2e-" + $suffix)
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$compose = "c:\Users\pavel\source\repos\SA-EKG\infra\docker-compose.yml"

function Write-Case([string]$Name, [bool]$Ok, [string]$Detail = "") {
    $status = if ($Ok) { "PASS" } else { "FAIL" }
    $results.Add([pscustomobject]@{ Case = $Name; Status = $status; Detail = $Detail })
    $color = if ($Ok) { "Green" } else { "Red" }
    $line = "[$status] $Name"
    if ($Detail) { $line = "$line - $Detail" }
    Write-Host $line -ForegroundColor $color
}

function Invoke-Json {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body = $null,
        [hashtable]$Headers = @{},
        [int[]]$ExpectStatus = @()
    )
    $params = @{
        Method      = $Method
        Uri         = $Url
        Headers     = $Headers
        ContentType = "application/json; charset=utf-8"
    }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
    }
    $code = 0
    $content = ""
    for ($attempt = 0; $attempt -lt 8; $attempt++) {
        try {
            $resp = Invoke-WebRequest @params -UseBasicParsing
            $content = $resp.Content
            $code = [int]$resp.StatusCode
        }
        catch {
            $web = $_.Exception.Response
            if ($null -eq $web) { throw }
            $code = [int]$web.StatusCode
            $stream = $web.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $content = $reader.ReadToEnd()
            $reader.Dispose()
        }
        if ($code -ne 429) { break }
        Start-Sleep -Seconds (2 * ($attempt + 1))
    }
    $json = $null
    if ($content) {
        try { $json = $content | ConvertFrom-Json } catch { $json = $null }
    }
    $obj = [pscustomobject]@{ StatusCode = $code; Content = $content; Json = $json }
    if ($ExpectStatus.Count -gt 0 -and ($ExpectStatus -notcontains $code)) {
        throw "Expected $($ExpectStatus -join ',') got $code for $Method $Url : $content"
    }
    return $obj
}

function New-AuthHeaders([string]$Token) {
    return @{ Authorization = "Bearer $Token" }
}

function New-EcgCsv {
    param(
        [int]$SampleCount = 2500,
        [int]$IntervalMs = 4,
        [int[]]$ErrorIndexes = @(),
        [hashtable]$GapsAfterIndexMs = @{},
        [scriptblock]$ValueSelector = $null
    )
    $errSet = @{}
    foreach ($i in $ErrorIndexes) { $errSet[$i] = $true }
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("timestamp,value,errors")
    $start = [datetime]::ParseExact("2026-01-01 08:00:00.000", "yyyy-MM-dd HH:mm:ss.fff", [cultureinfo]::InvariantCulture)
    $offset = [int64]0
    for ($i = 0; $i -lt $SampleCount; $i++) {
        if ($i -gt 0 -and $GapsAfterIndexMs.ContainsKey($i - 1)) {
            $offset += [int64]$GapsAfterIndexMs[$i - 1] - $IntervalMs
        }
        $ts = $start.AddMilliseconds($offset)
        if ($null -ne $ValueSelector) { $val = & $ValueSelector $i } else { $val = 100 }
        if ($errSet.ContainsKey($i)) { $err = "Error" } else { $err = "NoError" }
        [void]$sb.AppendLine(("{0},{1},{2}" -f $ts.ToString("yyyy-MM-dd HH:mm:ss.fff"), $val, $err))
        $offset += $IntervalMs
    }
    $path = Join-Path $tmp ("ecg-" + [guid]::NewGuid().ToString("N") + ".csv")
    [System.IO.File]::WriteAllText($path, $sb.ToString(), (New-Object System.Text.UTF8Encoding $false))
    return $path
}

function Invoke-Upload {
    param([string]$Token, [guid]$DeviceId, [string]$FilePath)
    $uri = "$ApiBase/api/v1/ecg-records"
    Add-Type -AssemblyName System.Net.Http
    $client = New-Object System.Net.Http.HttpClient
    $client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $Token)
    $form = New-Object System.Net.Http.MultipartFormDataContent
    $form.Add((New-Object System.Net.Http.StringContent($DeviceId.ToString())), "deviceId")
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    $fileContent = New-Object System.Net.Http.ByteArrayContent(,$bytes)
    $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("text/csv")
    $form.Add($fileContent, "file", [System.IO.Path]::GetFileName($FilePath))
    $response = $client.PostAsync($uri, $form).GetAwaiter().GetResult()
    $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $client.Dispose()
    $json = $null
    if ($text) { try { $json = $text | ConvertFrom-Json } catch { $json = $null } }
    return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Content = $text; Json = $json }
}

function Wait-RecordStatus {
    param([string]$Token, [guid]$RecordId, [string[]]$Statuses, [int]$TimeoutSec = 120)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $last = $null
    do {
        $r = Invoke-Json -Method GET -Url "$ApiBase/api/v1/ecg-records/$RecordId" -Headers (New-AuthHeaders $Token)
        $last = $r
        if ($r.StatusCode -eq 200 -and ($Statuses -contains $r.Json.status)) {
            return $r.Json
        }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    throw ("Timeout waiting for record {0} in [{1}], last={2}" -f $RecordId, ($Statuses -join ","), $last.Json.status)
}

Write-Host "=== SA-EKG e2e stand @ $ApiBase ===" -ForegroundColor Cyan

$health = Invoke-Json -Method GET -Url "$ApiBase/health" -ExpectStatus @(200)
Write-Case "health" ($health.StatusCode -eq 200) $health.Content

$adminLogin = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/login" -Body @{ email = $AdminEmail; password = $AdminPassword } -ExpectStatus @(200)
$adminToken = $adminLogin.Json.accessToken
$adminH = New-AuthHeaders $adminToken
Write-Case "admin login (seed)" ($null -ne $adminToken)

$device = Invoke-Json -Method POST -Url "$ApiBase/api/v1/admin/devices" -Headers $adminH -Body @{
    name         = "Holter-e2e-$suffix"
    serialNumber = "SN-E2E-$suffix"
} -ExpectStatus @(201)
$deviceId = [guid]$device.Json.id
Write-Case "admin create device" ($null -ne $deviceId) "$deviceId"

$patientEmail = "patient_${suffix}@example.com"
$patientPass = "Password1"
$reg = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/register" -Body @{ email = $patientEmail; password = $patientPass } -ExpectStatus @(200)
$patientId = [guid]$reg.Json.userId
$patientToken = $reg.Json.accessToken
$patientRefresh = $reg.Json.refreshToken
Write-Case "1a patient register" ($reg.StatusCode -eq 200) $patientEmail

$conflict = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/register" -Body @{ email = $patientEmail; password = $patientPass }
Write-Case "register conflict 409" ($conflict.StatusCode -eq 409)

$refresh1 = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/refresh" -Body @{ refreshToken = $patientRefresh } -ExpectStatus @(200)
$rotatedRefresh = $refresh1.Json.refreshToken
$reuse = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/refresh" -Body @{ refreshToken = $patientRefresh }
$reuseNew = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/refresh" -Body @{ refreshToken = $rotatedRefresh }
Write-Case "refresh reuse revokes chain" (($reuse.StatusCode -eq 401) -and ($reuseNew.StatusCode -eq 401)) ("reuse={0} chain={1}" -f $reuse.StatusCode, $reuseNew.StatusCode)

$relogin = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/login" -Body @{ email = $patientEmail; password = $patientPass } -ExpectStatus @(200)
$patientToken = $relogin.Json.accessToken
$patientH = New-AuthHeaders $patientToken

$csvOk = New-EcgCsv -SampleCount 2500
$noConsent = Invoke-Upload -Token $patientToken -DeviceId $deviceId -FilePath $csvOk
Write-Case "1b upload without consent -> 403" ($noConsent.StatusCode -eq 403)

Invoke-Json -Method POST -Url "$ApiBase/api/v1/admin/patients/$patientId/consent" -Headers $adminH -ExpectStatus @(204) | Out-Null
Write-Case "1c admin consent" $true

$doctorCreate = Invoke-Json -Method POST -Url "$ApiBase/api/v1/admin/users" -Headers $adminH -Body @{
    email = "doctor_${suffix}@example.com"
    role  = "Doctor"
} -ExpectStatus @(201)
$doctorId = [guid]$doctorCreate.Json.user.id
$doctorTemp = $doctorCreate.Json.temporaryPassword
$pdf = Invoke-Json -Method GET -Url "$ApiBase/api/v1/admin/users/$doctorId/temp-password.pdf" -Headers $adminH
$pdfGone = Invoke-Json -Method GET -Url "$ApiBase/api/v1/admin/users/$doctorId/temp-password.pdf" -Headers $adminH
Write-Case "7a create doctor + one-shot PDF" (($pdf.StatusCode -eq 200) -and ($pdfGone.StatusCode -eq 410)) ("pdf={0} gone={1}" -f $pdf.StatusCode, $pdfGone.StatusCode)

$doctorLoginTemp = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/login" -Body @{
    email    = "doctor_${suffix}@example.com"
    password = $doctorTemp
} -ExpectStatus @(200)
$doctorTokenTemp = $doctorLoginTemp.Json.accessToken
$mustChange = [bool]$doctorLoginTemp.Json.mustChangePassword
$doctorPatientsBlocked = Invoke-Json -Method GET -Url "$ApiBase/api/v1/doctor/patients" -Headers (New-AuthHeaders $doctorTokenTemp)
Write-Case "7b mustChangePassword blocks API" ($mustChange -and ($doctorPatientsBlocked.StatusCode -eq 403)) ("mustChange={0} patients={1}" -f $mustChange, $doctorPatientsBlocked.StatusCode)

Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/change-password" -Headers (New-AuthHeaders $doctorTokenTemp) -Body @{
    currentPassword = $doctorTemp
    newPassword     = "DoctorPass1"
} -ExpectStatus @(204) | Out-Null
$doctorLogin = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/login" -Body @{
    email    = "doctor_${suffix}@example.com"
    password = "DoctorPass1"
} -ExpectStatus @(200)
$doctorToken = $doctorLogin.Json.accessToken
$doctorH = New-AuthHeaders $doctorToken
Write-Case "7c password changed -> access open" (-not [bool]$doctorLogin.Json.mustChangePassword)

$foreignDoctor = Invoke-Json -Method POST -Url "$ApiBase/api/v1/admin/users" -Headers $adminH -Body @{
    email = "doctor2_${suffix}@example.com"
    role  = "Doctor"
} -ExpectStatus @(201)
$foreignTemp = $foreignDoctor.Json.temporaryPassword
$foreignLogin1 = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/login" -Body @{
    email    = "doctor2_${suffix}@example.com"
    password = $foreignTemp
}
Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/change-password" -Headers (New-AuthHeaders $foreignLogin1.Json.accessToken) -Body @{
    currentPassword = $foreignTemp
    newPassword     = "DoctorPass1"
} | Out-Null
$foreignToken = (Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/login" -Body @{
    email    = "doctor2_${suffix}@example.com"
    password = "DoctorPass1"
} -ExpectStatus @(200)).Json.accessToken
$foreignH = New-AuthHeaders $foreignToken

Invoke-Json -Method PUT -Url "$ApiBase/api/v1/admin/assignments" -Headers $adminH -Body @{
    doctorId  = $doctorId
    patientId = $patientId
} -ExpectStatus @(204) | Out-Null
Write-Case "assign doctor-patient" $true

$sexRu = ([string][char]0x043C + [string][char]0x0443 + [string][char]0x0436 + [string][char]0x0441 + [string][char]0x043A + [string][char]0x043E + [string][char]0x0439)
Invoke-Json -Method PUT -Url "$ApiBase/api/v1/medical-cards/$patientId" -Headers $doctorH -Body @{
    lastName   = "Ivanov"
    firstName  = "Ivan"
    middleName = "Ivanovich"
    birthDate  = "15.03.1990"
    sex        = $sexRu
} -ExpectStatus @(200) | Out-Null
Write-Case "1d medical card with sex (required for analysis)" $true

$upload = Invoke-Upload -Token $patientToken -DeviceId $deviceId -FilePath $csvOk
Write-Case "1e upload valid CSV -> 202" ($upload.StatusCode -eq 202) $upload.Content
$recordId = [guid]$upload.Json.recordId

$done = Wait-RecordStatus -Token $patientToken -RecordId $recordId -Statuses @("Done", "Failed") -TimeoutSec 180
Write-Case "1f analysis reaches Done" ($done.status -eq "Done") ("{0} failure={1}" -f $done.status, $done.failureCode)

$patientReportBefore = Invoke-Json -Method GET -Url "$ApiBase/api/v1/ecg-records/$recordId/report" -Headers $patientH
Write-Case "1g patient report before accept -> 404" ($patientReportBefore.StatusCode -eq 404)

$signalUrl = "$ApiBase/api/v1/ecg-records/$recordId/signal?fromMs=0&toMs=10000&maxPoints=500"
$doctorReport = Invoke-Json -Method GET -Url "$ApiBase/api/v1/ecg-records/$recordId/report" -Headers $doctorH -ExpectStatus @(200)
$doctorSignal = Invoke-Json -Method GET -Url $signalUrl -Headers $doctorH -ExpectStatus @(200)
Write-Case "2a assigned doctor sees report/signal" (($doctorReport.StatusCode -eq 200) -and ($doctorSignal.StatusCode -eq 200))

$foreignReport = Invoke-Json -Method GET -Url "$ApiBase/api/v1/ecg-records/$recordId/report" -Headers $foreignH
Write-Case "4a unassigned doctor -> 403" ($foreignReport.StatusCode -eq 403)

Invoke-Json -Method POST -Url "$ApiBase/api/v1/ecg-records/$recordId/report/accept" -Headers $doctorH -Body @{ comment = "ok" } -ExpectStatus @(204) | Out-Null
$patientReportAfter = Invoke-Json -Method GET -Url "$ApiBase/api/v1/ecg-records/$recordId/report" -Headers $patientH -ExpectStatus @(200)
$patientSignalAfter = Invoke-Json -Method GET -Url $signalUrl -Headers $patientH -ExpectStatus @(200)
Write-Case "2b after accept patient sees report/signal" (($patientReportAfter.StatusCode -eq 200) -and ($patientSignalAfter.StatusCode -eq 200))

$reportId = [guid]$doctorReport.Json.id
$a1 = [guid]::NewGuid()
$a2 = [guid]::NewGuid()
$sqlAnom = @"
INSERT INTO anomalies (id, report_id, indicator_code, direction, start_offset_ms, end_offset_ms, value_min, value_max, unit, reference_text, cycle_count)
VALUES
('$a1', '$reportId', 'HR', 'High', 0, 1000, 110, 120, 'bpm', '60-100', 1),
('$a2', '$reportId', 'PAUSE', 'High', 2000, 4000, 2.1, 2.1, 's', '>=2s', 1);
"@
docker compose -f $compose exec -T postgres psql -U saekg -d saekg -c $sqlAnom | Out-Null
$anomalyIds = @("$a1", "$a2")
$diag = Invoke-Json -Method POST -Url "$ApiBase/api/v1/diagnoses" -Headers $doctorH -Body @{
    patientId   = "$patientId"
    title       = "Sinus arrhythmia"
    description = "e2e"
    diagnosedOn = (Get-Date).ToString("dd.MM.yyyy")
    recordId    = "$recordId"
    reportId    = "$reportId"
    anomalyIds  = $anomalyIds
} -ExpectStatus @(201)
$diagnosisId = [guid]$diag.Json.id
$rec = Invoke-Json -Method POST -Url "$ApiBase/api/v1/diagnoses/$diagnosisId/recommendations" -Headers $doctorH -Body @{
    text    = "Reduce load"
    comment = $null
} -ExpectStatus @(201)
$rx = Invoke-Json -Method POST -Url "$ApiBase/api/v1/diagnoses/$diagnosisId/prescriptions" -Headers $doctorH -Body @{
    drugName  = "Drug-1"
    startDate = (Get-Date).ToString("dd.MM.yyyy")
    endDate   = (Get-Date).AddDays(7).ToString("dd.MM.yyyy")
    comment   = $null
} -ExpectStatus @(201)
$recGet = Invoke-Json -Method GET -Url "$ApiBase/api/v1/recommendations/$($rec.Json.id)" -Headers $patientH -ExpectStatus @(200)
$rxGet = Invoke-Json -Method GET -Url "$ApiBase/api/v1/prescriptions/$($rx.Json.id)" -Headers $patientH -ExpectStatus @(200)
$patientDiags = Invoke-Json -Method GET -Url "$ApiBase/api/v1/patients/$patientId/diagnoses" -Headers $patientH -ExpectStatus @(200)
$patientRecs = Invoke-Json -Method GET -Url "$ApiBase/api/v1/diagnoses/$diagnosisId/recommendations" -Headers $patientH -ExpectStatus @(200)
$patientRx = Invoke-Json -Method GET -Url "$ApiBase/api/v1/diagnoses/$diagnosisId/prescriptions" -Headers $patientH -ExpectStatus @(200)
$ok3 = ($diag.StatusCode -eq 201) -and ($rec.StatusCode -eq 201) -and ($rx.StatusCode -eq 201) -and ($recGet.StatusCode -eq 200) -and ($rxGet.StatusCode -eq 200) -and (@($patientDiags.Json).Count -ge 1) -and (@($patientRecs.Json).Count -ge 1) -and (@($patientRx.Json).Count -ge 1) -and (@($diag.Json.anomalyIds).Count -ge 2)
Write-Case "3 diagnosis + 2 anomalies + recommendation + prescription" $ok3 ("anomaliesBound={0}" -f @($diag.Json.anomalyIds).Count)

$patientB = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/register" -Body @{
    email    = "patientb_${suffix}@example.com"
    password = "Password1"
} -ExpectStatus @(200)
$patientBH = New-AuthHeaders $patientB.Json.accessToken
$foreignCard = Invoke-Json -Method GET -Url "$ApiBase/api/v1/medical-cards/$patientId" -Headers $patientBH
Write-Case "4b patient A vs B card -> 404" ($foreignCard.StatusCode -eq 404)

$badCsv = New-EcgCsv -SampleCount 2500 -ValueSelector { param($i) if ($i -eq 5) { 5001 } else { 100 } }
$badUpload = Invoke-Upload -Token $patientToken -DeviceId $deviceId -FilePath $badCsv
$badOk = ($badUpload.StatusCode -eq 422) -and ($badUpload.Json.code -eq "CSV_CHECK_FAILED") -and ($badUpload.Json.details.check -eq 5)
Start-Sleep -Seconds 2
$logs = Invoke-Json -Method GET -Url "$ApiBase/api/v1/admin/logs?module=CsvValidation&code=CSV_CHECK_5&page=1&pageSize=20" -Headers $adminH -ExpectStatus @(200)
$logHit = @($logs.Json.items) | Select-Object -First 1
Write-Case "5 CSV check 5 -> 422 + admin log" ($badOk -and ($null -ne $logHit)) ("status={0} line={1} logCode={2}" -f $badUpload.StatusCode, $badUpload.Json.details.line, $logHit.code)

$err31 = 0..774
$csv31 = New-EcgCsv -SampleCount 2500 -ErrorIndexes $err31
$up31 = Invoke-Upload -Token $patientToken -DeviceId $deviceId -FilePath $csv31
$err29 = 0..724
$gaps = @{ 100 = 40 }
$csv29 = New-EcgCsv -SampleCount 2500 -ErrorIndexes $err29 -GapsAfterIndexMs $gaps
$up29 = Invoke-Upload -Token $patientToken -DeviceId $deviceId -FilePath $csv29
$record29 = [guid]$up29.Json.recordId
$done29 = Wait-RecordStatus -Token $patientToken -RecordId $record29 -Statuses @("Done", "Failed") -TimeoutSec 180
$report29 = Invoke-Json -Method GET -Url "$ApiBase/api/v1/ecg-records/$record29/report" -Headers $doctorH
$gapsInReport = @($report29.Json.gaps).Count -gt 0
Write-Case "6 unusable 31% reject / 29% accept + gaps in report" (
    ($up31.StatusCode -eq 422) -and ($up31.Json.details.check -eq 12) -and ($up29.StatusCode -eq 202) -and ($done29.status -eq "Done") -and $gapsInReport
) ("31={0} 29={1} gaps={2}" -f $up31.StatusCode, $up29.StatusCode, @($report29.Json.gaps).Count)

$blockTarget = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/register" -Body @{
    email    = "block_${suffix}@example.com"
    password = "Password1"
} -ExpectStatus @(200)
$blockToken = $blockTarget.Json.accessToken
$blockId = [guid]$blockTarget.Json.userId
Invoke-Json -Method POST -Url "$ApiBase/api/v1/admin/users/$blockId/block" -Headers $adminH -ExpectStatus @(204) | Out-Null
$blockedCall = Invoke-Json -Method GET -Url "$ApiBase/api/v1/ecg-records" -Headers (New-AuthHeaders $blockToken)
Write-Case "8 block user -> tokens stop" (($blockedCall.StatusCode -eq 403) -or ($blockedCall.StatusCode -eq 401)) "$($blockedCall.StatusCode)"

$cardBefore = Invoke-Json -Method GET -Url "$ApiBase/api/v1/medical-cards/$patientId" -Headers $patientH -ExpectStatus @(200)
Invoke-Json -Method DELETE -Url "$ApiBase/api/v1/medical-cards/$patientId" -Headers $adminH -ExpectStatus @(204) | Out-Null
$cardAfter = Invoke-Json -Method GET -Url "$ApiBase/api/v1/medical-cards/$patientId" -Headers $patientH
Write-Case "9 soft delete medical card" (($cardBefore.StatusCode -eq 200) -and ($cardAfter.StatusCode -eq 404))
$sexRu = ([string][char]0x043C + [string][char]0x0443 + [string][char]0x0436 + [string][char]0x0441 + [string][char]0x043A + [string][char]0x043E + [string][char]0x0439)
Invoke-Json -Method PUT -Url "$ApiBase/api/v1/medical-cards/$patientId" -Headers $doctorH -Body @{
    lastName   = "Ivanov"
    firstName  = "Ivan"
    middleName = "Ivanovich"
    birthDate  = "15.03.1990"
    sex        = $sexRu
} -ExpectStatus @(200) | Out-Null

Write-Host "Preparing purge timestamps via SQL..." -ForegroundColor DarkGray
$sqlUpdate = "UPDATE ecg_records SET raw_purge_at = NOW() - INTERVAL '1 day', filtered_purge_at = NOW() - INTERVAL '1 day' WHERE id = '$recordId';"
docker compose -f $compose exec -T postgres psql -U saekg -d saekg -c $sqlUpdate | Out-Null
$verifyDue = docker compose -f $compose exec -T postgres psql -U saekg -d saekg -t -A -c "SELECT count(*) FROM ecg_records WHERE id = '$recordId' AND raw_purge_at < NOW() AND filtered_purge_at < NOW();"
Write-Host ("due rows: {0}" -f (($verifyDue | Out-String).Trim())) -ForegroundColor DarkGray
docker compose -f $compose restart api | Out-Null
for ($i = 0; $i -lt 30; $i++) {
    try {
        if ((Invoke-Json -Method GET -Url "$ApiBase/health").StatusCode -eq 200) { break }
    } catch {}
    Start-Sleep -Seconds 2
}
Start-Sleep -Seconds 20
$purgeDeadline = (Get-Date).AddSeconds(90)
$rawPurged = $false
$filteredPurged = $false
$reportStillThere = $false
do {
    $rawLine = (((docker compose -f $compose exec -T postgres psql -U saekg -d saekg -t -A -c "SELECT raw_purged FROM ecg_records WHERE id = '$recordId';") | Select-Object -Last 1) | Out-String).Trim()
    $filtLine = (((docker compose -f $compose exec -T postgres psql -U saekg -d saekg -t -A -c "SELECT filtered_purged FROM ecg_records WHERE id = '$recordId';") | Select-Object -Last 1) | Out-String).Trim()
    $repLine = (((docker compose -f $compose exec -T postgres psql -U saekg -d saekg -t -A -c "SELECT count(*) FROM analysis_reports WHERE record_id = '$recordId';") | Select-Object -Last 1) | Out-String).Trim()
    if ($rawLine -eq "t") { $rawPurged = $true }
    if ($filtLine -eq "t") { $filteredPurged = $true }
    if ($repLine -eq "1") { $reportStillThere = $true }
    if ($rawPurged -and $filteredPurged) { break }
    Start-Sleep -Seconds 5
} while ((Get-Date) -lt $purgeDeadline)
Write-Case "10 purge raw+filtered; report remains" ($rawPurged -and $filteredPurged -and $reportStillThere) ("raw={0} filtered={1} report={2}" -f $rawPurged, $filteredPurged, $reportStillThere)

$csvRestart = New-EcgCsv -SampleCount 2500
$upRestart = Invoke-Upload -Token $patientToken -DeviceId $deviceId -FilePath $csvRestart
$restartRecordId = [guid]$upRestart.Json.recordId
Start-Sleep -Seconds 1
docker compose -f $compose restart api | Out-Null
$healthy = $false
for ($i = 0; $i -lt 30; $i++) {
    try {
        $h = Invoke-Json -Method GET -Url "$ApiBase/health"
        if ($h.StatusCode -eq 200) { $healthy = $true; break }
    } catch {}
    Start-Sleep -Seconds 2
}
$afterRestart = Wait-RecordStatus -Token $patientToken -RecordId $restartRecordId -Statuses @("Done", "Failed") -TimeoutSec 180
Write-Case "11 restart mid-analyze resumes" ($healthy -and ($afterRestart.status -eq "Done")) $afterRestart.status

$lockEmail = "lock_${suffix}@example.com"
Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/register" -Body @{ email = $lockEmail; password = "Password1" } -ExpectStatus @(200) | Out-Null
$lastLock = $null
for ($i = 0; $i -lt 5; $i++) {
    $lastLock = Invoke-Json -Method POST -Url "$ApiBase/api/v1/auth/login" -Body @{ email = $lockEmail; password = "WrongPass1" }
}
Write-Case "lockout after 5 failed logins" ($lastLock.StatusCode -eq 423) "$($lastLock.StatusCode)"

Write-Case "security headers smoke" ((Invoke-Json -Method GET -Url "$ApiBase/health").StatusCode -eq 200)

$failed = @($results | Where-Object { $_.Status -eq "FAIL" })
$passed = $results.Count - $failed.Count
Write-Host ""
Write-Host ("=== Summary: {0}/{1} passed ===" -f $passed, $results.Count) -ForegroundColor $(if ($failed.Count -eq 0) { "Green" } else { "Yellow" })
$results | Format-Table -AutoSize | Out-Host
if ($failed.Count -gt 0) { exit 1 } else { exit 0 }
