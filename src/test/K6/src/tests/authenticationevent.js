import { check } from 'k6';
import { addErrorCount } from '../errorcounter.js';
import * as authenticationevent from '../api/authenticationevent.js';
import { generateJUnitXML, reportPath } from '../report.js';

export const options = {
  thresholds: {
    errors: ['count<1'],
  },
};

//Test for authentication event and validate response
export default function () {
  var res = authenticationevent.createauthenticationevent();
  console.log(res.status);
  var success = check(res, {
    'Create authentication event status is 201': (r) => r.status === 201,
  });
  addErrorCount(success);
}

export function handleSummary(data) {
  let result = {};
  result[reportPath('authenticationevent.xml')] = generateJUnitXML(data, 'auth-audit-log-authenticationevent');
  return result;
}