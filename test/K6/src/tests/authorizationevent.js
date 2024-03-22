import { check, sleep } from 'k6';
import { addErrorCount } from '../errorcounter.js';
import * as authorizationevent from '../api/authorizationevent.js';
import { generateJUnitXML, reportPath } from '../report.js';

export const options = {
  thresholds: {
    errors: ['count<1'],
  },
};

//Test for authorization event and validate response
export default function () {
  var body = [
    {
      created: '2023-06-14T02:39:37',
      subjectuserid: 20001337,
      resourcepartyid: 50001337,
      resource: 'app_skd_taxreport',
      operation: 'read',
      contextrequestjson: '{\"Attributes\":[{\"Id\":null,\"Content\":null,\"Category\":\"urn:oasis:names:tc:xacml:1.0:subject-category:access-subject\",\"Attributes\":[{\"Issuer\":null,\"AttributeId\":\"urn:altinn:userid\",\"AttributeValues\":[{\"Value\":\"20001337\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Elements\":[],\"Attributes\":[]}],\"IncludeInResult\":false}]},{\"Id\":null,\"Content\":null,\"Category\":\"urn:oasis:names:tc:xacml:3.0:attribute-category:action\",\"Attributes\":[{\"Issuer\":null,\"AttributeId\":\"urn:oasis:names:tc:xacml:1.0:action:action-id\",\"AttributeValues\":[{\"Value\":\"read\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Elements\":[],\"Attributes\":[]}],\"IncludeInResult\":false}]},{\"Id\":null,\"Content\":null,\"Category\":\"urn:oasis:names:tc:xacml:3.0:attribute-category:resource\",\"Attributes\":[{\"Issuer\":null,\"AttributeId\":\"urn:altinn:org\",\"AttributeValues\":[{\"Value\":\"org1\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Elements\":[],\"Attributes\":[]}],\"IncludeInResult\":false},{\"Issuer\":null,\"AttributeId\":\"urn:altinn:app\",\"AttributeValues\":[{\"Value\":\"app1\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Elements\":[],\"Attributes\":[]}],\"IncludeInResult\":false},{\"Issuer\":null,\"AttributeId\":\"urn:altinn:partyid\",\"AttributeValues\":[{\"Value\":\"50001337\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Elements\":[],\"Attributes\":[]}],\"IncludeInResult\":false}]}],\"XPathVersion\":null,\"CombinedDecision\":false,\"RequestReferences\":[],\"ReturnPolicyIdList\":false}',
      decision: 1
    },
  ];
  var res = authorizationevent.createauthorizationevent(body);
  console.log(res.status);
  var success = check(res, {
    'Create authorization event status is 200': (r) => r.status === 201,
  });
  addErrorCount(success);
  sleep(2);


  //Test for authorization event with bad request parameters
  body = [
    {
      created: '2023-06-14T02:39:37',
      subjectuserid: 20001337,
      resourcepartyid: 50001337,
      resource: 'app_skd_taxreport',
      operation: 'read',
      contextrequestjson: '{\"Attributes\":[{\"Id\":null,\"Content\":null,\"Category\":\"urn:oasis:names:tc:xacml:1.0:subject-category:access-subject\",\"Attributes\":[{\"Issuer\":null,\"AttributeId\":\"urn:altinn:userid\",\"AttributeValues\":[{\"Value\":\"20001337\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Elements\":[],\"Attributes\":[]}],\"IncludeInResult\":false}]},{\"Id\":null,\"Content\":null,\"Category\":\"urn:oasis:names:tc:xacml:3.0:attribute-category:action\",\"Attributes\":[{\"Issuer\":null,\"AttributeId\":\"urn:oasis:names:tc:xacml:1.0:action:action-id\",\"AttributeValues\":[{\"Value\":\"read\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Elements\":[],\"Attributes\":[]}],\"IncludeInResult\":false}]},{\"Id\":null,\"Content\":null,\"Category\":\"urn:oasis:names:tc:xacml:3.0:attribute-category:resource\",\"Attributes\":[{\"Issuer\":null,\"AttributeId\":\"urn:altinn:org\",\"AttributeValues\":[{\"Value\":\"org1\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Elements\":[],\"Attributes\":[]}],\"IncludeInResult\":false},{\"Issuer\":null,\"AttributeId\":\"urn:altinn:app\",\"AttributeValues\":[{\"Value\":\"app1\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Elements\":[],\"Attributes\":[]}],\"IncludeInResult\":false},{\"Issuer\":null,\"AttributeId\":\"urn:altinn:partyid\",\"AttributeValues\":[{\"Value\":\"50001337\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Elements\":[],\"Attributes\":[]}],\"IncludeInResult\":false}]}],\"XPathVersion\":null,\"CombinedDecision\":false,\"RequestReferences\":[],\"ReturnPolicyIdList\":false}',
      decision: 'Permit'
    },
  ];
  var res = authorizationevent.createauthorizationevent(body);
  console.log(res.status);
  var success = check(res, {
    'Create authorization event status is 400 bad request': (r) => r.status === 400,
  });
  addErrorCount(success);

}

export function handleSummary(data) {
  let result = {};
  result[reportPath('authorizationevent.xml')] = generateJUnitXML(data, 'auth-audit-log-authorizationevent');
  return result;
}