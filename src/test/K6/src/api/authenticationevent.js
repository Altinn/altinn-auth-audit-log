import http from 'k6/http';
import * as config from '../config.js';

// request to create an authentication event
export function createauthenticationevent() {
  var endpoint = config.auditLog.authenticationevent;
  var params = {
    headers: {
      'Content-Type': 'application/json',
    }
  };
  var body = [
    {
      timeStamp: '2023-06-14T12:39:37.881Z',
      userId: '10000',
      eventType: 'login',
      authenticationMethod: 'altinnpin',
      authenticationLevel: '2',
    },
  ];
  var bodystring = JSON.stringify(body);
  bodystring = bodystring.substring(1, bodystring.length-1)
  return http.post(endpoint, bodystring,params);
}
