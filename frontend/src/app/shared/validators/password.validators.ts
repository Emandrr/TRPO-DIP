import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export function passwordPolicyValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = String(control.value ?? '');
    if (value.length < 8) {
      return { passwordPolicy: true };
    }
    if (!/\p{L}/u.test(value) || !/\d/.test(value)) {
      return { passwordPolicy: true };
    }
    return null;
  };
}

export function matchFieldsValidator(a: string, b: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const left = group.get(a)?.value;
    const right = group.get(b)?.value;
    return left === right ? null : { passwordsMismatch: true };
  };
}
