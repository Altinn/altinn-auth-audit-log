-- Procedure: create_authenticationevent
CREATE OR REPLACE FUNCTION authentication.create_authenticationevent(
	_sessionid text,
	_created timestamp,
	_userid integer,
	_supplierid text,
	_orgnumber integer,
	_eventtypeid integer,
	_authenticationmethodid integer,
	_authenticationlevelid integer,
	_ipaddress text,
	_isauthenticated boolean)
    RETURNS authentication.eventlog
    LANGUAGE 'sql'
    COST 100
    VOLATILE PARALLEL SAFE 
AS $BODY$
INSERT INTO authentication.eventlog(
	sessionid,
	created,
	userid,
	supplierid,
	orgnumber,
	eventtypeid,	
	authenticationmethodid,
	authenticationlevelid,
	ipaddress,
	isauthenticated
)
VALUES (
	_sessionid,
	_created,
	_userid,
	_supplierid,
	_orgnumber,
	_eventtypeid,	
	_authenticationmethodid,
	_authenticationlevelid,
	_ipaddress,
	_isauthenticated
)
RETURNING *;
$BODY$;