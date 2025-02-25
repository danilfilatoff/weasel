using Shouldly;
using Weasel.Postgresql.Tables;
using Xunit;

namespace Weasel.Postgresql.Tests.Tables;

[Collection("tables")]
public class reading_existing_table_from_database: IntegrationContext
{
    public reading_existing_table_from_database(): base("tables")
    {
    }

    [Fact]
    public async Task read_columns()
    {
        await theConnection.OpenAsync();

        await theConnection.ResetSchemaAsync("tables");

        var table = new Table("people");
        table.AddColumn<int>("id").AsPrimaryKey();
        table.AddColumn<string>("first_name");
        table.AddColumn<string>("last_name");

        await CreateSchemaObjectInDatabase(table);

        var existing = await table.FetchExistingAsync(theConnection);

        existing.ShouldNotBeNull();
        existing.Identifier.ShouldBe(table.Identifier);
        existing.Columns.Count.ShouldBe(table.Columns.Count);

        for (int i = 0; i < table.Columns.Count; i++)
        {
            existing.Columns[i].Name.ShouldBe(table.Columns[i].Name);
            var existingType = existing.Columns[i].Type;
            var tableType = table.Columns[i].Type;

            PostgresqlProvider.Instance.ConvertSynonyms(existingType)
                .ShouldBe(PostgresqlProvider.Instance.ConvertSynonyms(tableType));
        }
    }

    [Fact]
    public async Task read_single_pk_field()
    {
        await theConnection.OpenAsync();

        await theConnection.ResetSchemaAsync("tables");

        var table = new Table("people");
        table.AddColumn<int>("id").AsPrimaryKey();
        table.AddColumn<string>("first_name");
        table.AddColumn<string>("last_name");

        await CreateSchemaObjectInDatabase(table);

        var existing = await table.FetchExistingAsync(theConnection);

        existing.PrimaryKeyColumns.Single()
            .ShouldBe("id");
    }

    [Fact]
    public async Task read_multi_pk_fields()
    {
        await theConnection.OpenAsync();

        await theConnection.ResetSchemaAsync("tables");

        var table = new Table("people");
        table.AddColumn<int>("id").AsPrimaryKey();
        table.AddColumn<string>("tenant_id").AsPrimaryKey();
        table.AddColumn<string>("first_name");
        table.AddColumn<string>("last_name");

        await CreateSchemaObjectInDatabase(table);

        var existing = await table.FetchExistingAsync(theConnection);

        existing.PrimaryKeyColumns.OrderBy(x => x)
            .ShouldBe(new[] { "id", "tenant_id" });
    }

    [PgVersionTargetedFact(MaximumVersion = "13.0")]
    public Task read_indexes_with_concurrent_index()=>
        read_indexes(true);

    [Fact]
    public Task read_indexes_without_concurrent_index() =>
        read_indexes(false);


    public async Task read_indexes(bool withConcurrentIndex)
    {
        await theConnection.OpenAsync();

        await theConnection.ResetSchemaAsync("tables");

        var states = new Table("states");
        states.AddColumn<int>("id").AsPrimaryKey();

        await CreateSchemaObjectInDatabase(states);

        var table = new Table("people");
        table.AddColumn<int>("id").AsPrimaryKey();
        table.AddColumn<string>("first_name").AddIndex();
        table.AddColumn<string>("last_name").AddIndex(i =>
        {
            i.IsConcurrent = withConcurrentIndex;
            i.Method = IndexMethod.hash;
        });

        table.AddColumn<int>("state_id").ForeignKeyTo(states, "id");

        await CreateSchemaObjectInDatabase(table);

        var existing = await table.FetchExistingAsync(theConnection);

        existing.Indexes.Count.ShouldBe(2);
    }

    [PgVersionTargetedFact(MaximumVersion = "13.0")]
    public Task read_fk_with_concurrent_index()=>
        read_fk(true);

    [Fact]
    public Task read_fk_without_concurrent_index() =>
        read_fk(false);

    public async Task read_fk(bool withConcurrentIndex)
    {
        await theConnection.OpenAsync();

        await theConnection.ResetSchemaAsync("tables");

        var states = new Table("states");
        states.AddColumn<int>("id").AsPrimaryKey();

        await CreateSchemaObjectInDatabase(states);

        var table = new Table("people");
        table.AddColumn<int>("id").AsPrimaryKey();
        table.AddColumn<string>("first_name").AddIndex();
        table.AddColumn<string>("last_name").AddIndex(i =>
        {
            i.IsConcurrent = withConcurrentIndex;
            i.Method = IndexMethod.hash;
        });

        table.AddColumn<int>("state_id").ForeignKeyTo(states, "id", onDelete: CascadeAction.Cascade,
            onUpdate: CascadeAction.Restrict);

        await CreateSchemaObjectInDatabase(table);

        var existing = await table.FetchExistingAsync(theConnection);

        var fk = existing.ForeignKeys.Single();

        fk.Name.ShouldBe("fkey_people_state_id");

        fk.ColumnNames.Single().ShouldBe("state_id");
        fk.LinkedNames.Single().ShouldBe("id");
        fk.LinkedTable.Name.ShouldBe("states");

        fk.OnDelete.ShouldBe(CascadeAction.Cascade);
        fk.OnUpdate.ShouldBe(CascadeAction.Restrict);
    }

    [PgVersionTargetedFact(MaximumVersion = "13.0")]
    public Task read_fk_with_multiple_columns_with_concurrent_index()=>
        read_fk_with_multiple_columns(true);

    [Fact]
    public Task read_fk_with_multiple_columns_without_concurrent_index() =>
        read_fk_with_multiple_columns(false);

    public async Task read_fk_with_multiple_columns(bool withConcurrentIndex)
    {
        await theConnection.OpenAsync();

        await theConnection.ResetSchemaAsync("tables");

        var states = new Table("states");
        states.AddColumn<int>("id").AsPrimaryKey();
        states.AddColumn<string>("tenant_id").AsPrimaryKey();

        await CreateSchemaObjectInDatabase(states);

        var table = new Table("people");
        table.AddColumn<int>("id").AsPrimaryKey();
        table.AddColumn<string>("first_name").AddIndex();
        table.AddColumn<string>("last_name").AddIndex(i =>
        {
            i.IsConcurrent = withConcurrentIndex;
            i.Method = IndexMethod.hash;
        });

        table.AddColumn<string>("tenant_id");
        table.AddColumn<int>("state_id");

        table.ForeignKeys.Add(new ForeignKey("fkey_people_state_id_tenant_id")
        {
            LinkedTable = states.Identifier,
            ColumnNames = new[] { "state_id", "tenant_id" },
            LinkedNames = new[] { "id", "tenant_id" },
            OnDelete = CascadeAction.Cascade,
            OnUpdate = CascadeAction.Restrict
        });

        await CreateSchemaObjectInDatabase(table);

        var existing = await table.FetchExistingAsync(theConnection);

        var fk = existing.ForeignKeys.Single();

        fk.Name.ShouldBe("fkey_people_state_id_tenant_id");

        fk.ColumnNames.ShouldBe(new[] { "state_id", "tenant_id" });
        fk.LinkedNames.ShouldBe(new[] { "id", "tenant_id" });
        fk.LinkedTable.Name.ShouldBe("states");
    }
}
