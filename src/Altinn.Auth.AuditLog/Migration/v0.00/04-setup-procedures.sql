-- Procedure: create_authenticationevent
CREATE OR REPLACE FUNCTION authentication.create_authenticationevent(
	_userid text,
	_supplierid text,
	_eventtype text,
	_orgnumber text,
	_authenticationmethod text,
	_authenticationlevel text,
	_sessionid text)
    RETURNS authentication.eventlog
    LANGUAGE 'sql'
    COST 100
    VOLATILE PARALLEL SAFE 
AS $BODY$
INSERT INTO authentication.eventlog(
	created,
	userid,
	supplierid,
	eventtype,
	orgnumber,
	authenticationmethod,
	authenticationlevel,
	sessionid
)
VALUES (
	Now(),
	_userid,
	_supplierid,
	_eventtype,
	_orgnumber,
	_authenticationmethod,
	_authenticationlevel,
	_sessionid
)
RETURNING *;
$BODY$;