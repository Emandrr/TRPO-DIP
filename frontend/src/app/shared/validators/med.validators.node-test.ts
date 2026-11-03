import assert from 'node:assert/strict';
import test from 'node:test';
import { passwordPolicyValid, sexRuValid } from './med.validators.ts';

test('passwordPolicyValid accepts letter+digit', () => {
  assert.equal(passwordPolicyValid('Password1'), true);
});

test('passwordPolicyValid rejects short', () => {
  assert.equal(passwordPolicyValid('Ab1'), false);
});

test('sexRuValid accepts russian values', () => {
  assert.equal(sexRuValid('мужской'), true);
  assert.equal(sexRuValid('женский'), true);
});

test('sexRuValid rejects unknown', () => {
  assert.equal(sexRuValid('other'), false);
});
