using System.Linq;
using System.Threading.Tasks;
using Baseline;
using Shouldly;
using Weasel.Postgresql.Functions;
using Weasel.Postgresql.Tables;
using Xunit;

namespace Weasel.Postgresql.Tests.Functions
{
    [Collection("functions")]
    public class FunctionTests : IntegrationContext
    {
        private readonly string theFunctionBody = @"
CREATE OR REPLACE FUNCTION functions.mt_get_next_hi(entity varchar) RETURNS integer AS
$$
DECLARE
    current_value bigint;
    next_value bigint;
BEGIN
    select hi_value into current_value from functions.mt_hilo where entity_name = entity;
    IF current_value is null THEN
        insert into functions.mt_hilo (entity_name, hi_value) values (entity, 0);
        next_value := 0;
    ELSE
        next_value := current_value + 1;
        update functions.mt_hilo set hi_value = next_value where entity_name = entity and hi_value = current_value;

        IF NOT FOUND THEN
            next_value := -1;
        END IF;
    END IF;

    return next_value;
END

$$ LANGUAGE plpgsql;
";
        
        private readonly string theDifferentBody = @"
CREATE OR REPLACE FUNCTION functions.mt_get_next_hi(entity varchar) RETURNS integer AS
$$
DECLARE
    current_value bigint;
    next_value bigint;
BEGIN
    update functions.mt_hilo set hi_value = next_value where entity_name = entity and hi_value = current_value;

    return 1;
END

$$ LANGUAGE plpgsql;
";

        private Table theHiloTable;

        public FunctionTests() : base("functions")
        {
            theHiloTable = new Table("functions.mt_hilo");
            theHiloTable.AddColumn<string>("entity_name").AsPrimaryKey();
            theHiloTable.AddColumn<int>("next_value");
            theHiloTable.AddColumn<int>("hi_value");
        }

        [Fact]
        public void can_read_the_function_identifier_from_a_function_body()
        {
            Function.ParseIdentifier(theFunctionBody)
                .ShouldBe(new DbObjectName("functions", "mt_get_next_hi"));
        }

        [Fact]
        public void can_derive_the_drop_statement_from_the_body()
        {
            var function = new Function(DbObjectName.Parse("functions.mt_get_next_hi"), theFunctionBody);
            function.DropStatements().Single().ShouldBe("drop function functions.mt_get_next_hi(varchar);");
        }

        [Fact]
        public void can_build_function_object_from_body()
        {
            var function = Function.ForSql(theFunctionBody);
            function.Identifier.ShouldBe(new DbObjectName("functions", "mt_get_next_hi"));
            
            function.DropStatements().Single()
                .ShouldBe("drop function functions.mt_get_next_hi(varchar);");
        }

        [Fact]
        public async Task can_build_function_in_database()
        {
            await ResetSchema();

            await CreateSchemaObjectInDatabase(theHiloTable);

            var function = Function.ForSql(theFunctionBody);

            await CreateSchemaObjectInDatabase(function);

            var next = await theConnection.CreateCommand("select functions.mt_get_next_hi('foo');")
                .ExecuteScalarAsync();
            
            next.As<int>().ShouldBe(0);
        }


        [Fact]
        public async Task can_fetch_the_existing()
        {
            await ResetSchema();

            await CreateSchemaObjectInDatabase(theHiloTable);

            var function = Function.ForSql(theFunctionBody);
            
            await CreateSchemaObjectInDatabase(function);

            var existing = await function.FetchExisting(theConnection);
            
            existing.Identifier.ShouldBe(new DbObjectName("functions", "mt_get_next_hi"));
            existing.DropStatements().Single().ShouldBe("DROP FUNCTION IF EXISTS functions.mt_get_next_hi(entity character varying);");
            existing.Body().ShouldNotBeNull();
        }

        [Fact]
        public async Task can_fetch_the_delta_when_the_existing_does_not_exist()
        {
            await ResetSchema();

            await CreateSchemaObjectInDatabase(theHiloTable);

            var function = Function.ForSql(theFunctionBody);
            
            (await function.FetchExisting(theConnection)).ShouldBeNull();

            var delta = await function.FetchDelta(theConnection);
            
            delta.Difference.ShouldBe(SchemaPatchDifference.Create);
        }
        
        [Fact]
        public async Task can_fetch_the_delta_when_the_existing_matches()
        {
            await ResetSchema();

            await CreateSchemaObjectInDatabase(theHiloTable);

            var function = Function.ForSql(theFunctionBody);

            await CreateSchemaObjectInDatabase(function);

            var delta = await function.FetchDelta(theConnection);
            
            delta.Difference.ShouldBe(SchemaPatchDifference.None);
        }

        [Fact]
        public async Task can_detect_a_change_in_body()
        {
            await ResetSchema();

            await CreateSchemaObjectInDatabase(theHiloTable);

            var function = Function.ForSql(theFunctionBody);
            await CreateSchemaObjectInDatabase(function);

            var different = Function.ForSql(theDifferentBody);

            var delta = await different.FetchDelta(theConnection);
            
            delta.Difference.ShouldBe(SchemaPatchDifference.Update);

        }
    }

}