import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

const RU_DATE = /^(\d{2})\.(\d{2})\.(\d{4})$/;

export function requiredLineValidator(max = 50): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = String(control.value ?? '').trim();
    if (value.length < 1 || value.length > max) {
      return { requiredLine: true };
    }
    return null;
  };
}

export function optionalMultilineValidator(max = 5000): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (control.value == null || control.value === '') {
      return null;
    }
    return String(control.value).length > max ? { maxLength: true } : null;
  };
}

export function ruDateValidator(opts?: { notFuture?: boolean }): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const raw = String(control.value ?? '').trim();
    const m = RU_DATE.exec(raw);
    if (!m) {
      return { ruDate: true };
    }
    const day = Number(m[1]);
    const month = Number(m[2]);
    const year = Number(m[3]);
    const date = new Date(Date.UTC(year, month - 1, day));
    if (
      date.getUTCFullYear() !== year ||
      date.getUTCMonth() !== month - 1 ||
      date.getUTCDate() !== day
    ) {
      return { ruDate: true };
    }
    if (opts?.notFuture) {
      const today = new Date();
      const todayUtc = Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate());
      if (date.getTime() > todayUtc) {
        return { futureDate: true };
      }
    }
    return null;
  };
}

export function sexRuValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    return sexRuValid(String(control.value ?? '')) ? null : { sexRu: true };
  };
}

export function sexRuValid(value: string): boolean {
  const v = value.trim();
  return v === 'мужской' || v === 'женский';
}

export function passwordPolicyValid(password: string): boolean {
  return password.length >= 8 && /[A-Za-zА-Яа-я]/.test(password) && /\d/.test(password);
}

export function prescriptionDatesValidator(startKey: string, endKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const start = String(group.get(startKey)?.value ?? '').trim();
    const end = String(group.get(endKey)?.value ?? '').trim();
    const parse = (s: string): Date | null => {
      const m = RU_DATE.exec(s);
      if (!m) {
        return null;
      }
      return new Date(Date.UTC(Number(m[3]), Number(m[2]) - 1, Number(m[1])));
    };
    const a = parse(start);
    const b = parse(end);
    if (!a || !b) {
      return null;
    }
    return b.getTime() < a.getTime() ? { endBeforeStart: true } : null;
  };
}

export function todayRuDate(): string {
  const d = new Date();
  const dd = String(d.getDate()).padStart(2, '0');
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const yyyy = d.getFullYear();
  return `${dd}.${mm}.${yyyy}`;
}
