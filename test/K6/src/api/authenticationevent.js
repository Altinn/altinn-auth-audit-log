import http from 'k6/http';
import * as config from '../config.js';

// request to create an authentication event
export function createauthenticationevent(body) {
  var endpoint = config.auditLog.authenticationevent;
  var params = {
    headers: {
      'Content-Type': 'application/json',
    }
  };
  var bodystring = JSON.stringify(body);
  bodystring = bodystring.substring(1, bodystring.length-1)
  return http.post(endpoint, bodystring,params);
}
