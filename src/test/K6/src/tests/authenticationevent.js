import { check, sleep } from 'k6';
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
  var body = [
    {
      created: '2023-06-14T02:39:37',
      userid: 10000,
      eventtype: 1,
      authenticationmethod: 0,
      authenticationlevel: 3,
    },
  ];
  var res = authenticationevent.createauthenticationevent(body);
  console.log(res.status);
  var success = check(res, {
    'Create authentication event status is 200': (r) => r.status === 201,
  });
  addErrorCount(success);
  sleep(2);


  //Test for authentication event with bad request parameters
  body = [
    {
      created: '2023-06-14T02:39:37',
      userid: 10000,
      eventtype: 'Authenticate',
      authenticationmethod: 0,
      authenticationlevel: 3,
    },
  ];
  var res = authenticationevent.createauthenticationevent(body);
  console.log(res.status);
  var success = check(res, {
    'Create authentication event status is 400 bad request': (r) => r.status === 400,
  });
  addErrorCount(success);

}

export function handleSummary(data) {
  let result = {};
  result[reportPath('authenticationevent.xml')] = generateJUnitXML(data, 'auth-audit-log-authenticationevent');
  return result;
}