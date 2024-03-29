// This file inhols baseURLs and endpoints for the APIs
export var baseUrls = {
  local: 'localhost:5166',
  at21: 'at21.altinn.cloud',
  at22: 'at22.altinn.cloud',
  at23: 'at23.altinn.cloud',
  at24: 'at24.altinn.cloud',
  tt02: 'tt02.altinn.no',
  yt01: 'yt01.altinn.cloud',
  prod: 'altinn.no',
};

//Get values from environment
const environment = __ENV.env.toLowerCase();
export let baseUrl = baseUrls[environment];
export let protocol;
if(environment == 'local')
{
  protocol = 'http://';
}
else
{
  protocol = 'https://';
}

//Audit log
export var auditLog = {
  authenticationevent: protocol + baseUrl + '/auditlog/api/v1/authenticationevent/',
  authorizationevent: protocol + baseUrl + '/auditlog/api/v1/authorizationevent/',
};
