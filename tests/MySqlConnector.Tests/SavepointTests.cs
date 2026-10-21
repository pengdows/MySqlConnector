namespace MySqlConnector.Tests;

public class SavepointTests : IDisposable
{
	public SavepointTests()
	{
		m_server = new();
		m_server.Start();

		m_csb = new()
		{
			Server = "localhost",
			Port = (uint) m_server.Port,
		};
	}

	public void Dispose()
	{
		m_server.Stop();
	}

	[Fact]
	public void MySqlSupportsSavepoints()
	{
		using var connection = new MySqlConnection(m_csb.ConnectionString);
		connection.Open();
		using var transaction = connection.BeginTransaction();
		Assert.True(transaction.SupportsSavepoints);
	}

	[Theory]
	[InlineData("5.5.5-10.2.44-MariaDB-1:10.2.44+maria~bionic")]
	[InlineData("11.4.13-MariaDB-ubu2404")]
	public void MariaDbSupportsSavepointsWithoutAsking(string serverVersion)
	{
		m_server.ServerVersion = serverVersion;
		using var connection = new MySqlConnection(m_csb.ConnectionString);
		connection.Open();
		using var transaction = connection.BeginTransaction();
		Assert.True(transaction.SupportsSavepoints);
		Assert.Equal(0, m_server.VersionCommentQueries);
	}

	[Theory]
	[InlineData("5.7.25-TiDB-v6.1.7", false)]
	[InlineData("5.7.25-TiDB-v6.2.0", true)]
	[InlineData("8.0.11-TiDB-v8.5.7", true)]
	public void TiDbSupportsSavepointsFromVersion62WithoutAsking(string serverVersion, bool expected)
	{
		m_server.ServerVersion = serverVersion;
		using var connection = new MySqlConnection(m_csb.ConnectionString);
		connection.Open();
		using var transaction = connection.BeginTransaction();
		Assert.Equal(expected, transaction.SupportsSavepoints);
		Assert.Equal(0, m_server.VersionCommentQueries);
	}

	[Fact]
	public void SingleStoreDoesNotSupportSavepoints()
	{
		// SingleStore reports a MySQL version and accepts SAVEPOINT, but ROLLBACK TO SAVEPOINT then fails with "SAVEPOINT does not exist"
		m_server.ServerVersion = "5.7.32";
		m_server.VersionComment = "SingleStoreDB source distribution (compatible; MySQL Enterprise & MySQL Commercial)";
		using var connection = new MySqlConnection(m_csb.ConnectionString);
		connection.Open();
		using var transaction = connection.BeginTransaction();
		Assert.False(transaction.SupportsSavepoints);
		Assert.Throws<NotSupportedException>(() => transaction.Save("a"));
		Assert.Throws<NotSupportedException>(() => transaction.Rollback("a"));
		Assert.Throws<NotSupportedException>(() => transaction.Release("a"));
	}

	[Fact]
	public async Task SingleStoreDoesNotSupportSavepointsAsync()
	{
		m_server.VersionComment = "SingleStoreDB source distribution (compatible; MySQL Enterprise & MySQL Commercial)";
		using var connection = new MySqlConnection(m_csb.ConnectionString);
		await connection.OpenAsync();
		using var transaction = await connection.BeginTransactionAsync();
		Assert.False(transaction.SupportsSavepoints);
		await Assert.ThrowsAsync<NotSupportedException>(() => transaction.SaveAsync("a"));
		await Assert.ThrowsAsync<NotSupportedException>(() => transaction.RollbackAsync("a"));
		await Assert.ThrowsAsync<NotSupportedException>(() => transaction.ReleaseAsync("a"));
	}

	[Fact]
	public void SavepointSupportIsDetectedOncePerPool()
	{
		m_csb.Pooling = true;
		using (var connection1 = new MySqlConnection(m_csb.ConnectionString))
		using (var connection2 = new MySqlConnection(m_csb.ConnectionString))
		{
			// two connections open at once use two sessions from the same pool
			connection1.Open();
			connection2.Open();
			Assert.Equal(2, m_server.ActiveConnections);
			using var transaction = connection2.BeginTransaction();
			Assert.True(transaction.SupportsSavepoints);
		}

		Assert.Equal(1, m_server.VersionCommentQueries);
	}

	[Fact]
	public void UnpooledConnectionsDoNotAsk()
	{
		m_csb.Pooling = false;
		using var connection = new MySqlConnection(m_csb.ConnectionString);
		connection.Open();
		using var transaction = connection.BeginTransaction();
		Assert.True(transaction.SupportsSavepoints);
		Assert.Equal(0, m_server.VersionCommentQueries);
	}

	private readonly FakeMySqlServer m_server;
	private readonly MySqlConnectionStringBuilder m_csb;
}
