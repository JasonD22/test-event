#nullable disable
#pragma warning disable IDE0063

using System.Diagnostics;
using Amazon.RDS.Util;
using Dapper;
using Npgsql;

namespace Events.API.Helpers
{
    public class DataContext
    {
        private readonly string _dbConfigurationKey = "ConnectionStrings:EventContext";

        private readonly DbSettings _dbSettings;

        public DataContext(IConfiguration configuration)
        {
            _dbSettings = new DbSettings();

            if (configuration[_dbConfigurationKey] is null)
            {
                return;
            }

            var dbProperties = configuration[_dbConfigurationKey].ToString().Split(';');

            foreach (var dbProperty in dbProperties)
            {
                var keyValue = dbProperty.Split('=');

                switch (keyValue[0].ToLower())
                {
                    case "host":
                        _dbSettings.Server = keyValue[1];
                        break;
                    case "database":
                        _dbSettings.Database = keyValue[1];
                        break;
                    case "username":
                        _dbSettings.Username = keyValue[1];
                        break;
                    case "password":
                        _dbSettings.Password = keyValue[1];
                        break;
                }
            }
        }

        public NpgsqlConnection CreateConnection(string databaseName = "")
        {
             var databaseToConnect = !String.IsNullOrWhiteSpace(databaseName) ? databaseName : _dbSettings.Database;
            var connectionString = $"Host={_dbSettings.Server}; Database={databaseToConnect}; Username={_dbSettings.Username}; Password={_dbSettings.Password}";

            var rdsEndpoint = Environment.GetEnvironmentVariable("rds_endpoint") ?? "RUN_LOCAL";

            // Is the code running on AWS?
            if (rdsEndpoint.Contains("amazonaws"))
            {
                if (rdsEndpoint.Contains(':'))
                    rdsEndpoint = rdsEndpoint.Substring(0, rdsEndpoint.IndexOf(':')).Replace(":", "");

                var pwd = RDSAuthTokenGenerator.GenerateAuthToken(rdsEndpoint, 5432, "syntax");

                connectionString = $"Host={rdsEndpoint}; Database={databaseToConnect}; Username={"syntax"}; Password={pwd}";

            }

            // Are we running locally but want to point at the AWS Postgres rds?
            if (rdsEndpoint == "127.0.0.1:7777")
            {
                connectionString = $"Host={rdsEndpoint}; Database={databaseToConnect}; Username={"selston"}; Password={"ENTER_THIS_HERE"}";
                Console.WriteLine("DataContext - CreateConnection -Using Local Conn");
            }
            return new NpgsqlConnection(connectionString);
        }

        public string GetConnectionString(string databaseName = "")
        {
            var databaseToConnect = !String.IsNullOrWhiteSpace(databaseName) ? databaseName : _dbSettings.Database;
            var connectionString = $"Host={_dbSettings.Server}; Database={databaseToConnect}; Username={_dbSettings.Username}; Password={_dbSettings.Password}";

            var rdsEndpoint = Environment.GetEnvironmentVariable("rds_endpoint") ?? "RUN_LOCAL";

            // Is the code running on AWS?
            if (rdsEndpoint.Contains("amazonaws"))
            {
                if (rdsEndpoint.Contains(':'))
                    rdsEndpoint = rdsEndpoint.Substring(0, rdsEndpoint.IndexOf(':')).Replace(":", "");

                var pwd = RDSAuthTokenGenerator.GenerateAuthToken(rdsEndpoint, 5432, "syntax");

                connectionString = $"Host={rdsEndpoint}; Database={databaseToConnect}; Username={"syntax"}; Password={pwd}";
            }

            // Are we running locally but want to point at the AWS Postgres rds?
            if (rdsEndpoint == "127.0.0.1:7777")
            {
                connectionString = $"Host={rdsEndpoint}; Database={databaseToConnect}; Username={"selston"}; Password={"ENTER_THIS_HERE"}";
                Console.WriteLine("DataContext - CreateConnection -Using Local Conn");
            }
            return connectionString + "; Pooling=false";
        }
        public async Task Init()
        {
            var dbCreated = await InitDatabase();

            if (dbCreated)
            {
                await InitTables();
                await InitFunctions();
                await SeedData();
            }
        }
        private async Task<bool> InitDatabase()
        {
            var dbCreated = false;

            using (var connection = CreateConnection("postgres"))
            {
                var sqlDbCount = $"SELECT COUNT(*) FROM pg_database WHERE datname = '{_dbSettings.Database}';";

                var dbCount = await connection.ExecuteScalarAsync<int>(sqlDbCount);

                if (dbCount == 0)
                {
                    var sql = $"CREATE DATABASE \"{_dbSettings.Database}\"";
                    var resp = await connection.ExecuteAsync(sql);
                    dbCreated = true;
                }

                connection.Close();
            }

            return dbCreated;
        }

        private async Task InitTables()
        {
            using (var connection = CreateConnection())
            {
                try
                {
                    await initStatusesTable();
                    await initOrganisersTable();
                    await initCategoriesTable();
                    await initGenresTable();
                    await initAddressesTable();
                    await initVenuesTable();
                    await initEventTable();
                    await initEventGenreTable();
                    await initEventImageTable();
                    await initSessionsTable();

                    async Task initEventTable()
                    {
                        var sql = """
                    CREATE TABLE IF NOT EXISTS public."Event"
                    (
                        "Id"                bigint NOT NULL GENERATED BY DEFAULT AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
                        "Name"              text COLLATE pg_catalog."default" NOT NULL,
                        "Description"       text COLLATE pg_catalog."default",
                        "FeaturedImage"     text COLLATE pg_catalog."default",
                        "Keywords"          text COLLATE pg_catalog."default",
                        "StatusId"          integer NOT NULL,
                        "PublishedBy"       uuid NOT NULL,
                        "PubliciseStart"    timestamp with time zone NOT NULL,
                        "PubliciseEnd"      timestamp with time zone NOT NULL,
                        "CategoryId"        integer,
                        "OrganiserId"       integer,
                        "Slug"              text COLLATE pg_catalog."default",
                        "PasswordEnabled"   boolean NOT NULL DEFAULT false, 
                        "MD5String"         text COLLATE pg_catalog."default",

                        CONSTRAINT "PK_Event" PRIMARY KEY ("Id"),
                        CONSTRAINT "FK_Event_Categories_CategoryId" FOREIGN KEY ("CategoryId")
                            REFERENCES public."Categories" ("Id") MATCH SIMPLE
                            ON UPDATE NO ACTION
                            ON DELETE NO ACTION,
                        CONSTRAINT "FK_Event_Organisers_OrganiserId" FOREIGN KEY ("OrganiserId")
                            REFERENCES public."Organisers" ("Id") MATCH SIMPLE
                            ON UPDATE NO ACTION
                            ON DELETE NO ACTION,
                        CONSTRAINT "FK_Event_Statuses_StatusId" FOREIGN KEY ("StatusId")
                            REFERENCES public."Statuses" ("Id") MATCH SIMPLE
                            ON UPDATE NO ACTION
                            ON DELETE CASCADE
                    )

                    TABLESPACE pg_default;

                    ALTER TABLE IF EXISTS public."Event"
                        OWNER to postgres;
                    -- Index: IX_Event_CategoryId

                    -- DROP INDEX IF EXISTS public."IX_Event_CategoryId";

                    CREATE INDEX IF NOT EXISTS "IX_Event_CategoryId"
                        ON public."Event" USING btree
                        ("CategoryId" ASC NULLS LAST)
                        TABLESPACE pg_default;
                    -- Index: IX_Event_OrganiserId

                    -- DROP INDEX IF EXISTS public."IX_Event_OrganiserId";

                    CREATE INDEX IF NOT EXISTS "IX_Event_OrganiserId"
                        ON public."Event" USING btree
                        ("OrganiserId" ASC NULLS LAST)
                        TABLESPACE pg_default;
                    -- Index: IX_Event_StatusId

                    -- DROP INDEX IF EXISTS public."IX_Event_StatusId";

                    CREATE INDEX IF NOT EXISTS "IX_Event_StatusId"
                        ON public."Event" USING btree
                        ("StatusId" ASC NULLS LAST)
                        TABLESPACE pg_default;
                    """;
                        await connection.ExecuteAsync(sql);
                    }
                    async Task initAddressesTable()
                    {
                        var sql = """
                        CREATE TABLE IF NOT EXISTS public."Addresses"
                        (
                            "Id" bigint NOT NULL GENERATED BY DEFAULT AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
                            "Name" text COLLATE pg_catalog."default",
                            "AddressLine1" text COLLATE pg_catalog."default",
                            "AddressLine2" text COLLATE pg_catalog."default",
                            "City" text COLLATE pg_catalog."default",
                            "County" text COLLATE pg_catalog."default",
                            "Postcode" text COLLATE pg_catalog."default",
                            "Country" text COLLATE pg_catalog."default",
                            CONSTRAINT "PK_Addresses" PRIMARY KEY ("Id")
                        )

                        TABLESPACE pg_default;

                        ALTER TABLE IF EXISTS public."Addresses"
                            OWNER to postgres;
                    """;
                        await connection.ExecuteAsync(sql);
                    }
                    async Task initCategoriesTable()
                    {
                        var sql = """

                    CREATE TABLE IF NOT EXISTS public."Categories"
                    (
                        "Id" integer NOT NULL GENERATED BY DEFAULT AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
                        "Name" text COLLATE pg_catalog."default",
                        CONSTRAINT "PK_Categories" PRIMARY KEY ("Id")
                    )

                    TABLESPACE pg_default;

                    ALTER TABLE IF EXISTS public."Categories"
                        OWNER to postgres;

                    """;
                        await connection.ExecuteAsync(sql);
                    }
                    async Task initEventGenreTable()
                    {
                        var sql = """

                    CREATE TABLE IF NOT EXISTS public."EventGenre"
                    (
                        "EventsId" bigint NOT NULL,
                        "GenresId" bigint NOT NULL,
                        CONSTRAINT "PK_EventGenre" PRIMARY KEY ("EventsId", "GenresId"),
                        CONSTRAINT "FK_EventGenre_Event_EventsId" FOREIGN KEY ("EventsId")
                            REFERENCES public."Event" ("Id") MATCH SIMPLE
                            ON UPDATE NO ACTION
                            ON DELETE CASCADE,
                        CONSTRAINT "FK_EventGenre_Genres_GenresId" FOREIGN KEY ("GenresId")
                            REFERENCES public."Genres" ("Id") MATCH SIMPLE
                            ON UPDATE NO ACTION
                            ON DELETE CASCADE
                    )

                    TABLESPACE pg_default;

                    ALTER TABLE IF EXISTS public."EventGenre"
                        OWNER to postgres;
                    -- Index: IX_EventGenre_GenresId

                    -- DROP INDEX IF EXISTS public."IX_EventGenre_GenresId";

                    CREATE INDEX IF NOT EXISTS "IX_EventGenre_GenresId"
                        ON public."EventGenre" USING btree
                        ("GenresId" ASC NULLS LAST)
                        TABLESPACE pg_default;
                    """;
                        await connection.ExecuteAsync(sql);
                    }
                    async Task initEventImageTable()
                    {
                        var sql = """

                    CREATE TABLE IF NOT EXISTS public."EventImage"
                    (
                        "Id" bigint NOT NULL GENERATED BY DEFAULT AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
                        "EventId" bigint NOT NULL,
                        "Name" text COLLATE pg_catalog."default",
                        "Uri" text COLLATE pg_catalog."default",
                        "Primary" boolean NOT NULL,
                        CONSTRAINT "PK_EventImage" PRIMARY KEY ("Id"),
                        CONSTRAINT "FK_EventImage_Event_EventId" FOREIGN KEY ("EventId")
                            REFERENCES public."Event" ("Id") MATCH SIMPLE
                            ON UPDATE NO ACTION
                            ON DELETE CASCADE
                    )

                    TABLESPACE pg_default;

                    ALTER TABLE IF EXISTS public."EventImage"
                        OWNER to postgres;
                    -- Index: IX_EventImage_EventId

                    -- DROP INDEX IF EXISTS public."IX_EventImage_EventId";

                    CREATE INDEX IF NOT EXISTS "IX_EventImage_EventId"
                        ON public."EventImage" USING btree
                        ("EventId" ASC NULLS LAST)
                        TABLESPACE pg_default;
                    """;
                        await connection.ExecuteAsync(sql);

                    }
                    async Task initGenresTable()
                    {
                        var sql = """

                    CREATE TABLE IF NOT EXISTS public."Genres"
                    (
                        "Id" bigint NOT NULL GENERATED BY DEFAULT AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
                        "Name" text COLLATE pg_catalog."default",
                        CONSTRAINT "PK_Genres" PRIMARY KEY ("Id")
                    )

                    TABLESPACE pg_default;

                    ALTER TABLE IF EXISTS public."Genres"
                        OWNER to postgres;

                    """;
                        await connection.ExecuteAsync(sql);

                    }
                    async Task initOrganisersTable()
                    {
                        var sql = """

                    CREATE TABLE IF NOT EXISTS public."Organisers"
                    (
                        "Id" integer NOT NULL GENERATED BY DEFAULT AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
                        CONSTRAINT "PK_Organisers" PRIMARY KEY ("Id")
                    )

                    TABLESPACE pg_default;

                    ALTER TABLE IF EXISTS public."Organisers"
                        OWNER to postgres;

                    """;
                        await connection.ExecuteAsync(sql);

                    }
                    async Task initSessionsTable()
                    {
                        var sql = """
                                        
                    CREATE TABLE IF NOT EXISTS public."Sessions"
                    (
                        "Id" bigint NOT NULL GENERATED BY DEFAULT AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
                        "EventId" bigint NOT NULL,
                        "VenueId" bigint NOT NULL,
                        "Start" timestamp with time zone NOT NULL,
                        "End" timestamp with time zone NOT NULL,
                        "MaxTickets" integer NOT NULL,
                        "IsSoldout" boolean NOT NULL,
                        "AdditionalInformation" text COLLATE pg_catalog."default",
                        "Description" text COLLATE pg_catalog."default",
                        "Information" text COLLATE pg_catalog."default",
                        "Name" text COLLATE pg_catalog."default",
                        "IsPaymentPlanAvailable" boolean NOT NULL DEFAULT false,
                        "PasswordEnabled"   boolean NOT NULL DEFAULT false, 
                        "MD5String"         text COLLATE pg_catalog."default",

                        CONSTRAINT "PK_Sessions" PRIMARY KEY ("Id"),
                        CONSTRAINT "FK_Sessions_Event_EventId" FOREIGN KEY ("EventId")
                            REFERENCES public."Event" ("Id") MATCH SIMPLE
                            ON UPDATE NO ACTION
                            ON DELETE CASCADE,
                        CONSTRAINT "FK_Sessions_Venues_VenueId" FOREIGN KEY ("VenueId")
                            REFERENCES public."Venues" ("Id") MATCH SIMPLE
                            ON UPDATE NO ACTION
                            ON DELETE CASCADE
                    )

                    TABLESPACE pg_default;

                    ALTER TABLE IF EXISTS public."Sessions"
                        OWNER to postgres;
                    -- Index: IX_Sessions_EventId

                    -- DROP INDEX IF EXISTS public."IX_Sessions_EventId";

                    CREATE INDEX IF NOT EXISTS "IX_Sessions_EventId"
                        ON public."Sessions" USING btree
                        ("EventId" ASC NULLS LAST)
                        TABLESPACE pg_default;
                    -- Index: IX_Sessions_VenueId

                    -- DROP INDEX IF EXISTS public."IX_Sessions_VenueId";

                    CREATE INDEX IF NOT EXISTS "IX_Sessions_VenueId"
                        ON public."Sessions" USING btree
                        ("VenueId" ASC NULLS LAST)
                        TABLESPACE pg_default;


                    """;
                        await connection.ExecuteAsync(sql);


                    }
                    async Task initStatusesTable()
                    {
                        var sql = """

                    CREATE TABLE IF NOT EXISTS public."Statuses"
                    (
                        "Id" integer NOT NULL GENERATED BY DEFAULT AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
                        "Name" text COLLATE pg_catalog."default",
                        CONSTRAINT "PK_Statuses" PRIMARY KEY ("Id")
                    )

                    TABLESPACE pg_default;

                    ALTER TABLE IF EXISTS public."Statuses"
                        OWNER to postgres;
                    """;
                        await connection.ExecuteAsync(sql);


                    }
                    async Task initVenuesTable()
                    {
                        var sql = """

                    CREATE TABLE IF NOT EXISTS public."Venues"
                    (
                        "Id" bigint NOT NULL GENERATED BY DEFAULT AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
                        "Name" text COLLATE pg_catalog."default",
                        "AddressId" bigint NOT NULL,
                        CONSTRAINT "PK_Venues" PRIMARY KEY ("Id"),
                        CONSTRAINT "FK_Venues_Addresses_AddressId" FOREIGN KEY ("AddressId")
                            REFERENCES public."Addresses" ("Id") MATCH SIMPLE
                            ON UPDATE NO ACTION
                            ON DELETE CASCADE
                    )

                    TABLESPACE pg_default;

                    ALTER TABLE IF EXISTS public."Venues"
                        OWNER to postgres;
                    -- Index: IX_Venues_AddressId

                    -- DROP INDEX IF EXISTS public."IX_Venues_AddressId";

                    CREATE INDEX IF NOT EXISTS "IX_Venues_AddressId"
                        ON public."Venues" USING btree
                        ("AddressId" ASC NULLS LAST)
                        TABLESPACE pg_default;
                    """;
                        await connection.ExecuteAsync(sql);


                    }
                }
                catch (Exception ex)
                {
                    connection.Close();
                    Debug.WriteLine("InitTables Error " + ex.ToString());
                    throw;
                }
               connection.Close();

            }
        }

        private async Task InitFunctions()
        {
            using (var connection = CreateConnection())
            {
                try
                {
                    await initGetEventSessionFunction();
                    await initGetEventsFunction();
                    await initGetEventFunction();
                    await initGetEventImages();

                    async Task initGetEventSessionFunction()
                    {
                        var sql = """
                    
                            CREATE OR REPLACE FUNCTION public.fngeteventsession(
                    	        p_eventid bigint,
                    	        p_sessionid  bigint)
                                RETURNS TABLE(sessionid bigint, eventid bigint, venueid bigint, starttime timestamp with time zone, endtime timestamp with time zone, sessionname text, description text, additionalinformation text, information text, maxtickets integer, issoldpout boolean, ispaymentplanavailable boolean, venuename text, addressid bigint, addressname text, addressline1 text, addressline2 text, city text, county text, postcode text, country text, passwordenabled boolean, md5string text) 
                                LANGUAGE 'plpgsql'
                                VOLATILE PARALLEL UNSAFE

                            AS $BODY$

                                BEGIN

                                RETURN QUERY
                                    SELECT 
                    					s."Id", 
                    	                s."EventId", 
                    	                s."VenueId", 
                    	                s."Start"       as starttime, 
                    	                s."End"         as endtime, 
                    	                s."Name", 
                    					s."Description", 
                    					s."AdditionalInformation", 
                    					s."Information", 			
                    					s."MaxTickets",
                    					s."IsSoldout",
                    					s."IsPaymentPlanAvailable", 
                    					v."Name", 

                    					a."Id",
                    					a."Name",
                    	                a."AddressLine1",
                    	                a."AddressLine2",
                    	                a."City",
                    	                a."County",
                    	                a."Postcode",
                    	                a."Country",

                                        s."PasswordEnabled",
                                        s."MD5String"

                                        FROM public."Sessions" s

                    	                LEFT JOIN public."Venues" v
                                            ON s."VenueId" = v."Id"

                                        LEFT JOIN public."Addresses" a
                                            ON s."VenueId" = a."Id"

                                    WHERE 	s."EventId" = p_eventid
                                    AND 	s."Id"      = p_sessionid;

                                END

                    $BODY$;

                    ALTER FUNCTION public.fngeteventsession(bigint, bigint)
                        OWNER TO postgres;
                    
                    """;

                        await connection.ExecuteAsync(sql);
                    }
                    async Task initGetEventsFunction()
                    {
                        var sql = """

                            CREATE OR REPLACE FUNCTION public.fngetevents()
                                RETURNS TABLE(
                            		eventid bigint, 
                            		eventname text, 
                            		slug text, 
                            		eventdescription text, 
                            		publicisestart timestamp with time zone, 
                            		publiciseend timestamp with time zone, 
                            		sessionid bigint, 
                            		sessionname text, 
                            		sessiondescription text, 
                            		information text, 
                            		additionalinformation text, 
                            		starttime timestamp with time zone, 
                            		endtime timestamp with time zone, 
                            		maxtickets integer, 
                            		issoldout boolean, 
                            		ispaymentplanavailable boolean, 
                            		venueid bigint, 
                            		venuename text, 
                            		addressid bigint, 
                            		addressname text, 
                            		addressline1 text, 
                            		addressline2 text, 
                            		city text, 
                            		county text, 
                            		postcode text, 
                            		country text,
                                    PasswordEnabled boolean,
                                    MD5String text,
                                    SessionPasswordEnabled boolean,
                                    SessionMD5String text
                            	) 
                                LANGUAGE 'plpgsql'
                                VOLATILE PARALLEL UNSAFE
                            AS $BODY$

                            BEGIN

                            RETURN QUERY
                            	SELECT 
                            		e."Id"						as "EventId",
                            		e."Name"					as "EventName",
                            		e."Slug"					as "Slug",
                            		e."Description"				as "EventDescription",
                            		e."PubliciseStart", 
                            		e."PubliciseEnd",
                            		s."Id" 						AS "SessionId",
                            		s."Name"					AS "SessionName",
                            		s."Description"				AS "SessionDescription",
                            		s."Information"				AS "Information",
                            		s."AdditionalInformation",
                            		s."Start"					AS "StartTime", 
                            		s."End"						AS "EndTime",
                            		s."MaxTickets",
                            		s."IsSoldout",
                            		s."IsPaymentPlanAvailable",
                            		v."Id"						AS "VenueId",
                            		v."Name"					AS "VenueName",
                            		a."Id"						AS "AddressId",
                            		a."Name"					AS "AddressName",
                            		a."AddressLine1",
                            		a."AddressLine2",
                            		a."City",
                            		a."County",
                            		a."Postcode",
                            		a."Country",
                                    e."PasswordEnabled",
                                    e."MD5String",
                                    s."PasswordEnabled"         AS "SessionPasswordEnabled",
                                    s."MD5String"               AS "SessionMD5String"

                            		FROM public."Sessions" s

                            		LEFT JOIN public."Event" e
                            			ON s."EventId"  = e."Id"

                            		LEFT JOIN public."Venues" v
                            			ON s."VenueId"  = v."Id"

                            		LEFT JOIN public."Addresses" a
                            			ON a."Id"  = v."AddressId"

                                    ORDER BY e."Id" ASC;

                            	END
                            $BODY$;

                            ALTER FUNCTION public.fngetevents()
                                OWNER TO postgres;
                            

                            """;

                        await connection.ExecuteAsync(sql);

                    }
                    async Task initGetEventFunction()
                    {
                        var sql = """


                            CREATE OR REPLACE FUNCTION public.fngetevent(
                              p_eventid bigint
                            )
                                RETURNS TABLE(
                            	eventid 				bigint, 
                            	eventname 				text, 
                            	slug 					text, 
                            	eventdescription 		text, 
                            	publicisestart 			timestamp with time zone, 
                            	publiciseend 			timestamp with time zone, 
                            	sessionid				bigint,
                            	sessionname 			text, 
                            	sessiondescription	 	text, 
                            	information 			text, 
                            	additionalinformation 	text,
                            	starttime 				timestamp with time zone, 
                            	endtime 				timestamp with time zone, 
                            	maxtickets				int,
                            	issoldout				boolean,
                            	ispaymentplanavailable	boolean,
                            	venueid					bigint,
                            	venuename				text,
                            	addressid				bigint,
                            	addressname				text,
                            	addressline1 			text, 
                            	addressline2 			text, 
                            	city 					text, 
                            	county 					text, 
                            	postcode 				text, 
                            	country 				text,
                                PasswordEnabled         boolean,
                                MD5String               text                                                                
                                ) 
                                LANGUAGE 'plpgsql'
                                VOLATILE PARALLEL UNSAFE

                            AS $BODY$

                            BEGIN

                            RETURN QUERY
                            	SELECT 
                            		e."Id"						as "EventId",
                            		e."Name"					as "EventName",
                            		e."Slug"					as "Slug",
                            		e."Description"				as "EventDescription",
                            		e."PubliciseStart", 
                            		e."PubliciseEnd",

                            		s."Id" 						AS "SessionId",
                            		s."Name"					AS "SessionName",
                            		s."Description"				AS "SessionDescription",
                            		s."Information"				AS "Information",
                            		s."AdditionalInformation",
                            		s."Start", 
                            		s."End",
                            		s."MaxTickets",
                            		s."IsSoldout",
                            		s."IsPaymentPlanAvailable",
                            		v."Id"						AS "VenueId",
                            		v."Name"					AS "VenueName",
                            		a."Id"						AS "AddressId",
                            		a."Name"					AS "AddressName",
                            		a."AddressLine1",
                            		a."AddressLine2",
                            		a."City",
                            		a."County",
                            		a."Postcode",
                            		a."Country",
                                    e."PasswordEnabled",
                                    e."MD5String"

                            		FROM public."Sessions" s

                            		LEFT JOIN public."Event" e
                            			ON s."EventId"  = e."Id"

                            		LEFT JOIN public."Venues" v
                            			ON s."VenueId"  = v."Id"

                            		LEFT JOIN public."Addresses" a
                            			ON a."Id"  = v."AddressId"

                                    WHERE e."Id" = p_eventid;

                            	END
                            $BODY$;

                            ALTER FUNCTION public.fngetevent(bigint)
                                OWNER TO postgres;
                            

                            """;

                        await connection.ExecuteAsync(sql);

                    }
                    async Task initGetEventImages()
                    {
                        var sql = """

                            CREATE OR REPLACE FUNCTION public.fngeteventimages(
                                p_eventid bigint
                            )
                                RETURNS TABLE(
                                imageid 			bigint, 
                                eventid				bigint, 
                                imagename 			text, 
                                imageuri 			text, 
                                primaryimage		boolean) 

                                LANGUAGE 'plpgsql'
                                VOLATILE PARALLEL UNSAFE

                            AS $BODY$

                            BEGIN

                            RETURN QUERY

                            	SELECT 
                            		"Id", 
                            		"EventId", 
                            		"Name", 
                            		"Uri", 
                            		"Primary"

                            	FROM public."EventImage"

                                WHERE "EventId" = p_eventid;

                                END
                            $BODY$;

                            ALTER FUNCTION public.fngeteventimages(bigint)
                                OWNER TO postgres;
                            
                            """;

                        await connection.ExecuteAsync(sql);

                    }
                }
                catch (Exception ex)
                {
                    connection.Close();
                    Debug.WriteLine("InitFunctions Error " + ex.ToString());
                    throw;
                }
                connection.Close();
            }
        }

        private async Task SeedData()
        {
            using (var connection = CreateConnection())
            {
                try
                {
                    await initAddressesData();
                    await initStatusesData();
                    await initVenuesData();
                    await initEventsData();
                    await initEventImageData();
                    await initSessionsData();

                    async Task initAddressesData()
                    {
                        var sql = $"""

                        INSERT INTO public."Addresses"
                        (
                            "Id", 
                            "Name", 
                            "AddressLine1", 
                            "AddressLine2", 
                            "City", 
                            "County", 
                            "Postcode", 
                            "Country"
                        )
                        VALUES 
                        (
                            1, 
                            'Green Man 2025', 
                            'Bannau Brycheiniog', 
                            null, 
                            null, 
                            null, 
                            'NP8', 
                            'Wales'
                        ),
                        (
                            2, 
                            'TOTFest 2025 Yorkshire!', 
                            'Wetherby Racecourse', 
                            'York Road', 
                            'Wetherby', 
                            'West Yorkshire', 
                            'LS22 5EJ', 
                            'UK'
                        ),
                        (
                            3, 
                            'TOTFest 2025 Suffolk!', 
                            'Newmarket Racecourse', 
                            'The Rowley Mile', 
                            'Newmarket', 
                            'Suffolk', 
                            'CB8 0TF', 
                            'UK'
                        ),
                        (
                            4, 
                            'TOTFest 2025 Surrey!', 
                            'Kempton Park Racecourse', 
                            'Staines Rd East', 
                            'Sunbury-on-Thames', 
                            'Surrey', 
                            'TW16 5AQ', 
                            'UK'
                        ),
                        (
                            5, 
                            'TOTFest 2025 Cheshire!', 
                            'Chester Racecourse', 
                            'New Crane St', 
                            '', 
                            'Chester', 
                            'CH1 4JD', 
                            'UK'
                        ),
                        (
                            6, 
                            'TOTFest 2025 Berkshire!', 
                            'Windsor Racecourse', 
                            'Maidenhead Road', 
                            'Windsor', 
                            'Berkshire', 
                            'SL4 5EZ', 
                            'UK'
                        );

                    """;

                        await connection.ExecuteAsync(sql);
                    }
                    async Task initStatusesData()
                    {
                        var sql = $"""

                    INSERT INTO public."Statuses"
                    (
                        "Id", 
                        "Name"
                    )
                    VALUES 
                    (
                        1, 
                        'Draft'
                    ),                     
                    (
                        2, 
                        'Live'
                    );

                    """;

                        await connection.ExecuteAsync(sql);
                    }
                    async Task initVenuesData()
                    {
                        var sql = $"""

                    INSERT INTO public."Venues"
                    (
                        "Id", 
                        "Name", 
                        "AddressId"
                    )
                    VALUES 
                    (
                        1, 
                        'Bannau Brycheiniog',
                        1
                    ),
                    (
                        2, 
                        'Wetherby Racecourse',
                        2
                    ),
                    (
                        3, 
                        'Newmarket Racecourse',
                        3
                    ),
                    (
                        4, 
                        'Kempton Park Showground',
                        4
                    ),
                    (
                        5, 
                        'Chester Racecourse',
                        5
                    ),
                    (
                        6, 
                        'Windsor Racecourse',
                        6
                    );

                    """;

                        await connection.ExecuteAsync(sql);
                    }
                    async Task initEventsData()
                    {
                        var sql = $"""

                    INSERT INTO public."Event"
                    (
                        "Id", 
                        "Name", 
                        "StatusId",
                        "PublishedBy",
                        "PubliciseStart",
                        "PubliciseEnd",
                        "PasswordEnabled",
                        "MD5String"
                    )
                    VALUES 
                    (
                        1, 
                        'Green Man 2025 Festival',              
                        2,
                        '00000000-0000-0000-0000-000000000000'::uuid,
                        '-infinity',
                        '-infinity',
                        true,
                        'b014fa995eeeee081e3d87a5b6413039'
                    ),
                    (
                        2, 
                        'TOTFest 2025',              
                        2,
                        '00000000-0000-0000-0000-000000000000'::uuid,
                        '2024-08-28 08:00GMT',
                        '2025-08-28 08:00GMT',
                        false,
                        ''
                    );

                    """;

                        await connection.ExecuteAsync(sql);
                    }
                    async Task initEventImageData()
                    {
                        var sql = $"""

                    INSERT INTO public."EventImage"
                    (
                        "Id", 
                        "EventId",
                        "Name", 
                        "Uri", 
                        "Primary"
                    )
                    VALUES
                    (
                        1, 
                        2, 
                        'TOTFest Logo', 
                        '/assets/totfest/images/totfest-header.png', 
                        true
                    ),
                    (
                        2, 
                        1, 
                        'Greenman', 
                        '/assets/greenman/images/gm-logo-line.gif', 
                        true
                    );

                    """;

                        await connection.ExecuteAsync(sql);
                    }
                    async Task initSessionsData()
                    {
                        var sql = $"""

                    INSERT INTO public."Sessions"
                    (
                        "Id", 
                        "EventId", 
                        "VenueId", 
                        "Start", 
                        "End", 
                        "MaxTickets", 
                        "IsSoldout", 
                        "AdditionalInformation", 
                        "Description", 
                        "Information", 
                        "Name", 
                        "IsPaymentPlanAvailable",
                        "PasswordEnabled",
                        "MD5String"
                    )
                    VALUES 
                    (
                        1, 
                        1, 
                        1, 
                        '2024-08-11 23:00:00+00', 
                        '2024-08-11 23:00:00+00', 
                        20, 
                        false, 
                        null, 
                        'Settler/Weekend Session', 
                        null, 
                        'Green Man 2025 Settler/Weekend Tickets', 
                        true,
                        false,
                        ''
                    ),
                    (
                        2, 
                        2, 
                        2, 
                        '2025-05-18 12:00:00+00', 
                        '2025-05-18 17:00:00+00', 
                        20, 
                        false, 
                        null, 
                        'TOTFest® is an entirely ALL INCLUSIVE festival, meaning your entry ticket includes all advertised shows, sessions, rides and activities. This includes the Fairground and Bounceland.', 
                        '
                        <ul>
                            <li>TOTFest® is an entirely ALL INCLUSIVE festival, meaning your entry ticket includes all advertised shows, sessions, rides and activities. This includes the Fairground and Bounceland. </li>
                            <li>Carparks open from 10am | Festival Entry from 12pm - please note there are no toilet facilities in the car park. </li>
                            <li>All cars require a parking permit, these are advance booking only. </li>
                            <li>Due to the nature of this event, any adult without an accompanying child will be refused admission (please ensure on the system that people are still able to buy a single adult ticket if they wish though). </li>
                            <li>Tickets will be posted to your registered address one month before event. </li>
                            <li>Full terms and conditions can be found here: <a href="http://www.totfestfestival.com/totfest2025terms" target="_blank">TOTFEST 2025 Terms</a>. </li>
                        </ul>
                        ', 
                        'TOTFest 2025 - Yorkshire!', 
                        true,
                        false,
                        ''
                    ),
                    (
                        3, 
                        2, 
                        3, 
                        '2025-06-08 12:00:00+00', 
                        '2025-06-08 17:00:00+00', 
                        20, 
                        false, 
                        null, 
                        'TOTFest® is an entirely ALL INCLUSIVE festival, meaning your entry ticket includes all advertised shows, sessions, rides and activities. This includes the Fairground and Bounceland.', 
                        '
                        <ul>
                            <li>TOTFest® is an entirely ALL INCLUSIVE festival, meaning your entry ticket includes all advertised shows, sessions, rides and activities. This includes the Fairground and Bounceland. </li>
                            <li>Carparks open from 10am | Festival Entry from 12pm - please note there are no toilet facilities in the car park. </li>
                            <li>All cars require a parking permit, these are advance booking only. </li>
                            <li>Due to the nature of this event, any adult without an accompanying child will be refused admission (please ensure on the system that people are still able to buy a single adult ticket if they wish though). </li>
                            <li>Tickets will be posted to your registered address one month before event. </li>
                            <li>Full terms and conditions can be found here: <a href="http://www.totfestfestival.com/totfest2025terms" target="_blank">TOTFEST 2025 Terms</a>. </li>
                        </ul>
                        ',
                        'TOTFest 2025 - Suffolk!', 
                        true,
                        false,
                        ''
                    ),
                    (
                        4, 
                        2, 
                        4, 
                        '2025-06-22 12:00:00+00', 
                        '2025-06-22 17:00:00+00', 
                        20, 
                        false, 
                        null, 
                        'TOTFest® is an entirely ALL INCLUSIVE festival, meaning your entry ticket includes all advertised shows, sessions, rides and activities. This includes the Fairground and Bounceland.', 
                        '
                        <ul>
                            <li> TOTFest® is an entirely ALL INCLUSIVE festival, meaning your entry ticket includes all advertised shows, sessions, rides and activities. This includes the Fairground and Bounceland. </li>
                            <li> Carparks open from 10am | Festival Entry from 12pm - please note there are no toilet facilities in the car park. </li>
                            <li> All cars require a parking permit, these are advance booking only. </li>
                            <li> Due to the nature of this event, any adult without an accompanying child will be refused admission (please ensure on the system that people are still able to buy a single adult ticket if they wish though). </li>
                            <li> Tickets will be posted to your registered address one month before event. </li>
                            <li>Full terms and conditions can be found here: <a href="http://www.totfestfestival.com/totfest2025terms" target="_blank">TOTFEST 2025 Terms</a>. </li>
                        </ul>
                        ',
                        'TOTFest 2025 - Surrey!', 
                        true,
                        false,
                        ''                        
                    ),
                    (
                        5, 
                        2, 
                        5, 
                        '2025-07-06 12:00:00+00', 
                        '2025-07-06 17:00:00+00', 
                        20, 
                        false, 
                        null, 
                        'TOTFest® is an entirely ALL INCLUSIVE festival, meaning your entry ticket includes all advertised shows, sessions, rides and activities. This includes the Fairground and Bounceland.', 
                        '
                        <ul>
                            <li> TOTFest® is an entirely ALL INCLUSIVE festival, meaning your entry ticket includes all advertised shows, sessions, rides and activities. This includes the Fairground and Bounceland. </li>
                            <li> Carparks open from 10am | Festival Entry from 12pm - please note there are no toilet facilities in the car park. </li>
                            <li> All cars require a parking permit, these are advance booking only. </li>
                            <li> Due to the nature of this event, any adult without an accompanying child will be refused admission (please ensure on the system that people are still able to buy a single adult ticket if they wish though). </li>
                            <li> Tickets will be posted to your registered address one month before event. </li>
                            <li>Full terms and conditions can be found here: <a href="http://www.totfestfestival.com/totfest2025terms" target="_blank">TOTFEST 2025 Terms</a>. </li>
                        </ul>
                        ',
                        'TOTFest 2025 - Cheshire!', 
                        true,
                        false,
                        ''                        
                    ),
                    (
                        6, 
                        2, 
                        6, 
                        '2025-07-20 12:00:00+00', 
                        '2025-07-20 17:00:00+00', 
                        20, 
                        false, 
                        null, 
                        'TOTFest® is an entirely ALL INCLUSIVE festival, meaning your entry ticket includes all advertised shows, sessions, rides and activities. This includes the Fairground and Bounceland.', 
                        '
                        <ul>
                            <li> TOTFest® is an entirely ALL INCLUSIVE festival, meaning your entry ticket includes all advertised shows, sessions, rides and activities. This includes the Fairground and Bounceland. </li>
                            <li> Carparks open from 10am | Festival Entry from 12pm - please note there are no toilet facilities in the car park. </li>
                            <li> All cars require a parking permit, these are advance booking only. </li>
                            <li> Due to the nature of this event, any adult without an accompanying child will be refused admission (please ensure on the system that people are still able to buy a single adult ticket if they wish though). </li>
                            <li> Tickets will be posted to your registered address one month before event. </li>
                            <li>Full terms and conditions can be found here: <a href="http://www.totfestfestival.com/totfest2025terms" target="_blank">TOTFEST 2025 Terms</a>. </li>
                        </ul>
                        ',
                        'TOTFest 2025 - Berkshire!', 
                        true,
                        false,
                        ''                        
                    ),
                    (
                        7, 
                        1, 
                        1, 
                        '2024-08-15 23:00:00+00', 
                        '2024-08-15 23:00:00+00', 
                        20, 
                        false, 
                        null, 
                        'Day Session', 
                        null, 
                        'Green Man 2025 Resident Day Tickets', 
                        false,
                        true,
                        'ef92c42bba75c4e5e4bc90fae1cc1dd0'
                    );

                    """;

                        await connection.ExecuteAsync(sql);
                    }
                }
                catch (Exception ex)
                {
                    connection.Close();
                    Debug.WriteLine("SeedData Error " + ex.ToString());
                    throw;
                }
                connection.Close();
            }
        }
    }
}