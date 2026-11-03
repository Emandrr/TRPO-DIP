CREATE EXTENSION IF NOT EXISTS citext;

CREATE TABLE users (
    id                   uuid PRIMARY KEY,
    email                citext NOT NULL,
    password_hash        text NOT NULL,
    role                 text NOT NULL,
    is_blocked           boolean NOT NULL DEFAULT false,
    must_change_password boolean NOT NULL DEFAULT false,
    failed_login_count   integer NOT NULL DEFAULT 0,
    locked_until         timestamptz NULL,
    created_at           timestamptz NOT NULL,
    updated_at           timestamptz NOT NULL,
    CONSTRAINT ck_users_role CHECK (role IN ('Patient', 'Doctor', 'Admin'))
);

CREATE UNIQUE INDEX ix_users_email ON users (email);

CREATE TABLE refresh_tokens (
    id          uuid PRIMARY KEY,
    user_id     uuid NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    token_hash  text NOT NULL,
    expires_at  timestamptz NOT NULL,
    revoked_at  timestamptz NULL,
    replaced_by uuid NULL
);

CREATE INDEX ix_refresh_tokens_user_id ON refresh_tokens (user_id);

CREATE TABLE patients (
    user_id           uuid PRIMARY KEY REFERENCES users (id) ON DELETE CASCADE,
    consent_signed    boolean NOT NULL DEFAULT false,
    consent_signed_at timestamptz NULL
);

CREATE TABLE doctors (
    user_id uuid PRIMARY KEY REFERENCES users (id) ON DELETE CASCADE
);

CREATE TABLE medical_cards (
    id          uuid PRIMARY KEY,
    patient_id  uuid NOT NULL REFERENCES patients (user_id) ON DELETE CASCADE,
    last_name   text NULL,
    first_name  text NULL,
    middle_name text NULL,
    birth_date  date NOT NULL,
    sex         text NOT NULL,
    is_deleted  boolean NOT NULL DEFAULT false,
    deleted_at  timestamptz NULL,
    deleted_by  uuid NULL,
    CONSTRAINT ck_medical_cards_sex CHECK (sex IN ('male', 'female'))
);

CREATE UNIQUE INDEX ix_medical_cards_patient_id ON medical_cards (patient_id);

CREATE TABLE doctor_patient (
    doctor_id   uuid NOT NULL REFERENCES doctors (user_id) ON DELETE CASCADE,
    patient_id  uuid NOT NULL REFERENCES patients (user_id) ON DELETE CASCADE,
    assigned_at timestamptz NOT NULL,
    assigned_by uuid NOT NULL,
    PRIMARY KEY (doctor_id, patient_id)
);

CREATE INDEX ix_doctor_patient_patient_id ON doctor_patient (patient_id);

CREATE TABLE devices (
    id            uuid PRIMARY KEY,
    name          text NOT NULL,
    serial_number text NOT NULL,
    model         text NULL,
    description   text NULL,
    is_deleted    boolean NOT NULL DEFAULT false,
    deleted_at    timestamptz NULL,
    deleted_by    uuid NULL
);

CREATE UNIQUE INDEX ix_devices_serial_number ON devices (serial_number);

CREATE TABLE ecg_records (
    id                uuid PRIMARY KEY,
    patient_id        uuid NOT NULL REFERENCES patients (user_id) ON DELETE CASCADE,
    device_id         uuid NOT NULL REFERENCES devices (id) ON DELETE CASCADE,
    uploaded_at       timestamptz NOT NULL,
    original_filename text NOT NULL,
    sample_count      bigint NOT NULL,
    started_at        timestamp(3) WITHOUT TIME ZONE NOT NULL,
    ended_at          timestamp(3) WITHOUT TIME ZONE NOT NULL,
    status            text NOT NULL,
    failure_code      text NULL,
    raw_purge_at      timestamptz NOT NULL,
    raw_purged        boolean NOT NULL DEFAULT false,
    filtered_purge_at timestamptz NULL,
    filtered_purged   boolean NOT NULL DEFAULT false,
    unusable_ratio    numeric(5, 4) NOT NULL,
    CONSTRAINT ck_ecg_records_status CHECK (
        status IN ('Uploaded', 'Validating', 'Queued', 'Processing', 'Done', 'Failed')
    ),
    CONSTRAINT ck_ecg_records_sample_count CHECK (sample_count >= 0),
    CONSTRAINT ck_ecg_records_time_range CHECK (ended_at >= started_at),
    CONSTRAINT ck_ecg_records_unusable_ratio CHECK (unusable_ratio >= 0 AND unusable_ratio <= 1),
    CONSTRAINT ck_ecg_records_failure_code CHECK (
        status = 'Failed' OR failure_code IS NULL
    )
);

CREATE INDEX ix_ecg_records_patient_id_uploaded_at
    ON ecg_records (patient_id, uploaded_at);

CREATE INDEX ix_ecg_records_device_id ON ecg_records (device_id);

CREATE TABLE ecg_gaps (
    id              uuid PRIMARY KEY,
    record_id       uuid NOT NULL REFERENCES ecg_records (id) ON DELETE CASCADE,
    start_offset_ms bigint NOT NULL,
    end_offset_ms   bigint NOT NULL,
    reason          text NOT NULL,
    CONSTRAINT ck_ecg_gaps_reason CHECK (reason IN ('Error', 'Gap')),
    CONSTRAINT ck_ecg_gaps_offsets CHECK (
        start_offset_ms >= 0 AND end_offset_ms >= start_offset_ms
    )
);

CREATE INDEX ix_ecg_gaps_record_id ON ecg_gaps (record_id);

CREATE TABLE analysis_reports (
    id               uuid PRIMARY KEY,
    record_id        uuid NOT NULL REFERENCES ecg_records (id) ON DELETE CASCADE,
    created_at       timestamptz NOT NULL,
    status           text NOT NULL,
    decided_by       uuid NULL REFERENCES users (id) ON DELETE SET NULL,
    decided_at       timestamptz NULL,
    decision_comment text NULL,
    summary          jsonb NOT NULL,
    CONSTRAINT ck_analysis_reports_status
        CHECK (status IN ('Pending', 'Accepted', 'Rejected'))
);

CREATE UNIQUE INDEX ix_analysis_reports_record_id ON analysis_reports (record_id);
CREATE INDEX ix_analysis_reports_decided_by ON analysis_reports (decided_by);

CREATE TABLE anomalies (
    id              uuid PRIMARY KEY,
    report_id       uuid NOT NULL REFERENCES analysis_reports (id) ON DELETE CASCADE,
    indicator_code  text NOT NULL,
    direction       text NOT NULL,
    start_offset_ms bigint NOT NULL,
    end_offset_ms   bigint NOT NULL,
    value_min       numeric(18, 6) NULL,
    value_max       numeric(18, 6) NULL,
    unit            text NULL,
    reference_text  text NULL,
    cycle_count     integer NOT NULL,
    CONSTRAINT ck_anomalies_indicator_code CHECK (
        indicator_code IN (
            'HR', 'RR', 'RR_DEV', 'PAUSE', 'R_AMP',
            'QRS_SHAPE', 'QRS_DUR', 'PR', 'P_WAVE',
            'QT', 'QTC', 'ST', 'T_WAVE'
        )
    ),
    CONSTRAINT ck_anomalies_direction CHECK (
        direction IN ('Low', 'High', 'Absent', 'Wrong')
    ),
    CONSTRAINT ck_anomalies_offsets CHECK (
        start_offset_ms >= 0 AND end_offset_ms >= start_offset_ms
    ),
    CONSTRAINT ck_anomalies_values CHECK (
        value_min IS NULL OR value_max IS NULL OR value_min <= value_max
    ),
    CONSTRAINT ck_anomalies_cycle_count CHECK (cycle_count >= 0)
);

CREATE INDEX ix_anomalies_report_id ON anomalies (report_id);

CREATE TABLE diagnoses (
    id           uuid PRIMARY KEY,
    patient_id   uuid NOT NULL REFERENCES patients (user_id) ON DELETE CASCADE,
    doctor_id    uuid NOT NULL REFERENCES doctors (user_id) ON DELETE CASCADE,
    record_id    uuid NULL REFERENCES ecg_records (id) ON DELETE SET NULL,
    report_id    uuid NULL REFERENCES analysis_reports (id) ON DELETE SET NULL,
    title        varchar(50) NOT NULL,
    description  varchar(5000) NULL,
    diagnosed_on date NOT NULL,
    is_deleted   boolean NOT NULL DEFAULT false,
    deleted_at   timestamptz NULL,
    deleted_by   uuid NULL,
    created_at   timestamptz NOT NULL,
    updated_at   timestamptz NOT NULL
);

CREATE INDEX ix_diagnoses_patient_id ON diagnoses (patient_id);
CREATE INDEX ix_diagnoses_doctor_id ON diagnoses (doctor_id);
CREATE INDEX ix_diagnoses_record_id ON diagnoses (record_id);
CREATE INDEX ix_diagnoses_report_id ON diagnoses (report_id);

CREATE TABLE diagnosis_anomalies (
    diagnosis_id uuid NOT NULL REFERENCES diagnoses (id) ON DELETE CASCADE,
    anomaly_id   uuid NOT NULL REFERENCES anomalies (id) ON DELETE CASCADE,
    PRIMARY KEY (diagnosis_id, anomaly_id)
);

CREATE INDEX ix_diagnosis_anomalies_anomaly_id ON diagnosis_anomalies (anomaly_id);

CREATE TABLE recommendations (
    id           uuid PRIMARY KEY,
    diagnosis_id uuid NOT NULL REFERENCES diagnoses (id) ON DELETE CASCADE,
    text         varchar(5000) NOT NULL,
    comment      varchar(5000) NULL,
    is_deleted   boolean NOT NULL DEFAULT false,
    deleted_at   timestamptz NULL,
    deleted_by   uuid NULL,
    created_at   timestamptz NOT NULL,
    updated_at   timestamptz NOT NULL
);

CREATE INDEX ix_recommendations_diagnosis_id ON recommendations (diagnosis_id);

CREATE TABLE prescriptions (
    id           uuid PRIMARY KEY,
    diagnosis_id uuid NOT NULL REFERENCES diagnoses (id) ON DELETE CASCADE,
    drug_name    varchar(50) NOT NULL,
    start_date   date NOT NULL,
    end_date     date NOT NULL,
    comment      varchar(5000) NULL,
    is_deleted   boolean NOT NULL DEFAULT false,
    deleted_at   timestamptz NULL,
    deleted_by   uuid NULL,
    created_at   timestamptz NOT NULL,
    updated_at   timestamptz NOT NULL,
    CONSTRAINT ck_prescriptions_dates CHECK (end_date >= start_date)
);

CREATE INDEX ix_prescriptions_diagnosis_id ON prescriptions (diagnosis_id);

CREATE TABLE system_logs (
    id         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ts         timestamptz NOT NULL,
    level      text NOT NULL,
    module     text NOT NULL,
    code       text NULL,
    message    text NOT NULL,
    user_id    uuid NULL,
    request_id text NULL,
    details    jsonb NULL
);

CREATE INDEX ix_system_logs_ts_level_module ON system_logs (ts, level, module);

CREATE TABLE background_jobs (
    id           uuid PRIMARY KEY,
    type         text NOT NULL,
    payload      jsonb NOT NULL,
    status       text NOT NULL,
    attempts     integer NOT NULL,
    run_after    timestamptz NOT NULL,
    locked_by    text NULL,
    locked_until timestamptz NULL,
    last_error   text NULL
);

CREATE TABLE audit_access (
    id        uuid PRIMARY KEY,
    ts        timestamptz NOT NULL,
    user_id   uuid NOT NULL,
    action    text NOT NULL,
    entity    text NOT NULL,
    entity_id uuid NOT NULL
);

CREATE TABLE raw_samples (
    record_id UUID NOT NULL,
    offset_ms BIGINT NOT NULL,
    ts        TIMESTAMP NOT NULL,
    value     SMALLINT NOT NULL,
    PRIMARY KEY (record_id, offset_ms)
);

CREATE TABLE filtered_samples (
    record_id UUID NOT NULL,
    offset_ms BIGINT NOT NULL,
    value     REAL NOT NULL,
    PRIMARY KEY (record_id, offset_ms)
);

CREATE TABLE fiducials (
    record_id    UUID NOT NULL,
    cycle_idx    INTEGER NOT NULL,
    segment_idx  INTEGER NOT NULL,
    r_ms         BIGINT NOT NULL,
    q_on_ms      BIGINT,
    s_off_ms     BIGINT,
    p_on_ms      BIGINT,
    p_off_ms     BIGINT,
    t_off_ms     BIGINT,
    j_ms         BIGINT,
    baseline_uv  REAL,
    r_amp_uv     REAL,
    PRIMARY KEY (record_id, cycle_idx, segment_idx)
);

CREATE INDEX ix_raw_samples_record_id ON raw_samples (record_id);
CREATE INDEX ix_filtered_samples_record_id ON filtered_samples (record_id);
CREATE INDEX ix_fiducials_record_id ON fiducials (record_id);
