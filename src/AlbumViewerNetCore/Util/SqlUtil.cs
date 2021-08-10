using System;
using System.IO;
using System.Data.SQLite;
using AlbumViewerBusiness;

namespace AlbumViewerNetCore
{
	public class SqlUtil
	{
		string connectionString = null;

		public SqlUtil()
		{
			this.connectionString = "Data Source=" + Path.Combine(Directory.GetCurrentDirectory(), "AlbumViewerData.sqlite") + ";Version=3;";
		}

		public void CreateCommand(string queryString)
		{
			using (SQLiteConnection connection = new SQLiteConnection(connectionString))
			{
				SQLiteCommand command = new SQLiteCommand(queryString, connection);
				command.Connection.Open();
				command.ExecuteNonQuery();
			}
		}

		public void InsertTrackSafe(Track track)
		{
			string insertQuery = "INSERT INTO Tracks (Id, AlbumId, SongName, Length, Bytes, UnitPrice) VALUES (NULL, @AlbumId, @SongName, @Length, @Bytes, @UnitPrice)";

			using (SQLiteConnection connection = new SQLiteConnection(connectionString))
			{
				SQLiteCommand command = new SQLiteCommand(insertQuery, connection);

				command.Parameters.AddWithValue("@AlbumId", track.AlbumId);
				command.Parameters.AddWithValue("@SongName", track.SongName);
				command.Parameters.AddWithValue("@Length", track.Length);
				command.Parameters.AddWithValue("@Bytes", track.Bytes);
				command.Parameters.AddWithValue("@UnitPrice", track.UnitPrice);

				try
				{
					command.Connection.Open();
					Int32 rowsAffected = command.ExecuteNonQuery();
					Console.WriteLine("RowsAffected: {0}", rowsAffected);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			}
		}
	}
}
