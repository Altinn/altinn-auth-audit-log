-- Procedure: create_authenticationevent
CREATE OR REPLACE FUNCTION authentication.create_authenticationevent(
	_created timestamp,
	_userid integer,
	_supplierid text,
	_eventtype text,
	_orgnumber integer,
	_authenticationmethod text,
	_authenticationlevel text,
	_ipaddress text,
	_isauthenticated boolean,
	_timetodelete timestamp)
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
	ipaddress,
	isauthenticated,
	timetodelete
)
VALUES (
	_created,
	_userid,
	_supplierid,
	_eventtype,
	_orgnumber,
	_authenticationmethod,
	_authenticationlevel,
	_ipaddress,
	_isauthenticated,
	_timetodelete
)
RETURNING *;
$BODY$;