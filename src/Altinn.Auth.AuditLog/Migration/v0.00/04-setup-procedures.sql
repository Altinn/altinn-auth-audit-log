-- Procedure: create_authenticationevent
CREATE OR REPLACE FUNCTION authentication.create_authenticationevent(
	_authenticationeventjson jsonb)
    RETURNS authentication.eventlog
    LANGUAGE 'sql'
    COST 100
    VOLATILE PARALLEL SAFE 
AS $BODY$
INSERT INTO authentication.eventlog(
	created,
	modified,
	authenticationeventjson
)
VALUES (
	Now(),
	Now(),
	_authenticationeventjson
)
RETURNING *;
$BODY$;