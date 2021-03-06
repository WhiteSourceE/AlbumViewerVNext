using AlbumViewerBusiness;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AlbumViewerNetCore;
using Microsoft.Data.Sqlite;
//using System.Data.SQLite;



// For more information on enabling MVC for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace AlbumViewerAspNetCore
{
	[ServiceFilter(typeof(ApiExceptionFilter))]    
	public class AlbumViewerApiController : Controller
	{
		AlbumViewerContext context;
		IServiceProvider serviceProvider;
		
		ArtistRepository ArtistRepo;
		AlbumRepository AlbumRepo;
		IConfiguration Configuration;
		private ILogger<AlbumViewerApiController> Logger;

		private IWebHostEnvironment HostingEnv;

		public AlbumViewerApiController(
			AlbumViewerContext ctx, 
			IServiceProvider svcProvider,
			ArtistRepository artistRepo, 
			AlbumRepository albumRepo, 
			IConfiguration config,
			ILogger<AlbumViewerApiController> logger,
			IWebHostEnvironment env)
		{
			context = ctx;
			serviceProvider = svcProvider;
			Configuration = config;

			AlbumRepo = albumRepo;
			ArtistRepo = artistRepo;
			Logger = logger;

			HostingEnv = env;
		}

	  


		[HttpGet]
		[Route("api/throw")]
		public object Throw()
		{
			throw new InvalidOperationException("This is an unhandled exception");            
		}

		
		#region albums

		[HttpGet]
		[Route("api/albums")]
		public async Task<IEnumerable<Album>> GetAlbums(int page = -1, int pageSize = 15)
		{
			//var repo = new AlbumRepository(context);
			return await AlbumRepo.GetAllAlbums(page, pageSize);
		}

		[HttpGet("api/album/title/unsafe/{title}")]
		public Album GetAlbumByTitleUnsafe(string title)
		{
			var albumByTitle = context.Albums.FromSqlRaw("SELECT * FROM Albums Where Title = " + title).FirstOrDefault();
			return albumByTitle;
		}

		[HttpGet("api/album/title/safe/{title}")]
		public Album GetAlbumByTitleSafe(string title)
		{
			var albumByTitle = context.Albums.FromSqlRaw("SELECT * FROM Albums Where Title = {0}", title).FirstOrDefault();
			return albumByTitle;
		}

		[HttpPost("api/track/sqlite/unsafe")]
		public string InsertTrackUnsafe([FromBody] Track postedTrack)
		{
			string insertQuery = new Track().BuildInsertQuery(postedTrack);
			SqlUtil sqlUtil = new SqlUtil();
			sqlUtil.CreateCommand(insertQuery);
			return "ok";
		}

		[HttpGet("api/album/{id:int}")]
		public async Task<Album> GetAlbum(int id)
		{
			return await AlbumRepo.Load(id);
		}

		[HttpGet("api/album/title/repo/unsafe/{title}")]
		public async Task<Album> GetAlbumByTitle(string title)
		{
			return await AlbumRepo.LoadWithRawSql(title);
		}

		[HttpPost("api/track/sqlite/safe")]
		public string InsertTrackSafe([FromBody] Track postedTrack)
		{
			SqlUtil sqlUtil = new SqlUtil();
			sqlUtil.InsertTrackSafe(postedTrack);
			return "ok";
		}

		[HttpGet("api/album/title/xss/{title}")]
		public string GetAlbumByTitleXss(string title)
		{
			return title;
		}

		[HttpPost("api/album")]
		public async Task<Album> SaveAlbum([FromBody] Album postedAlbum)
		{
			//throw new ApiException("Lemmy says: NO!");

			if (!HttpContext.User.Identity.IsAuthenticated)
				throw new ApiException("You have to be logged in to modify data", 401);

			if (!ModelState.IsValid)
				throw new ApiException("Model binding failed.", 500);

			if (!AlbumRepo.Validate(postedAlbum))
				throw new ApiException(AlbumRepo.ErrorMessage, 500, AlbumRepo.ValidationErrors);

			// this doesn't work for updating the child entities properly
			//if(!await AlbumRepo.SaveAsync(postedAlbum))
			//    throw new ApiException(AlbumRepo.ErrorMessage, 500);

			var album = await AlbumRepo.SaveAlbum(postedAlbum);
			if (album == null)
				throw new ApiException(AlbumRepo.ErrorMessage, 500);

			return album;
		}

		[HttpDelete("api/album/{id:int}")]
		public async Task<bool> DeleteAlbum(int id)
		{
			if (!HttpContext.User.Identity.IsAuthenticated)
				throw new ApiException("You have to be logged in to modify data", 401);

			return await AlbumRepo.DeleteAlbum(id);
		}


		[HttpGet]
		public async Task<string> DeleteAlbumByName(string name)
		{
			if (!HttpContext.User.Identity.IsAuthenticated)
				throw new ApiException("You have to be logged in to modify data", 401);

			var pks =
				await context.Albums
					.Where(alb => alb.Title == name)
					.Select(alb => alb.Id).ToListAsync();

			StringBuilder sb = new StringBuilder();
			foreach (int pk in pks)
			{
				bool result = await AlbumRepo.DeleteAlbum(pk);
				if (!result)
					sb.AppendLine(AlbumRepo.ErrorMessage);
			}

			return sb.ToString();
		}

		#endregion

		#region artists

		[HttpGet]
		[Route("api/artists")]
		public async Task<IEnumerable> GetArtists()
		{
			return await ArtistRepo.GetAllArtists();
		}

		[HttpGet("api/artist/{id:int}")]
		public async Task<object> Artist(int id)
		{
			var artist = await ArtistRepo.Load(id);

			if (artist == null)
				throw new ApiException("Invalid artist id.", 404);

			var albums = await ArtistRepo.GetAlbumsForArtist(id);

			return new ArtistResponse()
			{
				Artist = artist,
				Albums = albums
			};
		}

		[HttpPost("api/artist")]
		public async Task<ArtistResponse> SaveArtist([FromBody] Artist artist)
		{
			if (!HttpContext.User.Identity.IsAuthenticated)
				throw new ApiException("You have to be logged in to modify data", 401);

			if (!ArtistRepo.Validate(artist))
				throw new ApiException(ArtistRepo.ValidationErrors.ToString(), 500, ArtistRepo.ValidationErrors);

			//if (artist.Id < 1)
			//    ArtistRepo.Context.Artists.Add(artist);
			//else
			//     ArtistRepo.Context.Artists.Update(artist);

			if (!await ArtistRepo.SaveAsync(artist))
				throw new ApiException($"Unable to save artist. {ArtistRepo.ErrorMessage}");

			return new ArtistResponse()
			{
				Artist = artist,
				Albums = await ArtistRepo.GetAlbumsForArtist(artist.Id)
			};
		}

		[HttpGet("api/artistlookup")]
		public async Task<IEnumerable<object>> ArtistLookup(string search = null)
		{
			if (string.IsNullOrEmpty(search))
				return new List<object>();

			var repo = new ArtistRepository(context);
			var term = search.ToLower();
			return await repo.ArtistLookup(term);
		}


		[HttpDelete("api/artist/{id:int}")]
		public async Task<bool> DeleteArtist(int id)
		{
			if (!HttpContext.User.Identity.IsAuthenticated)
				throw new ApiException("You have to be logged in to modify data", 401);

			return await ArtistRepo.DeleteArtist(id);
		}

		#endregion

		#region admin
		[HttpGet]
		[Route("api/reloaddata")]
		public bool ReloadData()
		{
			if (!HttpContext.User.Identity.IsAuthenticated)
				throw new ApiException("You have to be logged in to modify data", 401);

			string isSqLite = Configuration["data:useSqLite"];
			try
			{
				if (isSqLite != "true")
				{
					// ExecuteSqlRaw // in EF 3.0
					context.Database.ExecuteSqlRaw(@"
													drop table Tracks;
													drop table Albums;
													drop table Artists;
													drop table Users;
													");
				}
				else
				{
					// this is not reliable for mutliple connections
					context.Database.CloseConnection();

					try
					{
						System.IO.File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "AlbumViewerData.sqlite"));
					}
					catch
					{
						throw new ApiException("Can't reset data. Existing database is busy.");
					}
				}

			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}


			AlbumViewerDataImporter.EnsureAlbumData(context,
				Path.Combine(HostingEnv.ContentRootPath, 
				"albums.js"));

			return true;
		}


		[HttpGet]
		[Route("api/insert/table/unsafe/{tableName}")]
		public string InsertTable(string tableName)
		{
			try
			{
				
					// ExecuteSqlRaw // in EF 3.0
					context.Database.ExecuteSqlRaw(@"
														CREATE TABLE " + tableName + " (" +
														"	contact_id INTEGER PRIMARY KEY, " + 
														"	first_name TEXT NOT NULL, " + 
														"	last_name TEXT NOT NULL, " + 
														"	email TEXT NOT NULL UNIQUE, " + 
														"	phone TEXT NOT NULL UNIQUE )");
				

			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}

			return "table created";
		}

		[HttpGet]
		[Route("api/update/table/safe/{id:int}/{title}")]
		public string InsertTableSafe(int id, string title)
		{
			try
			{
				string sql = @"UPDATE Albums SET Title = {0} WHERE Id = {1}";
				context.Database.ExecuteSqlRaw(sql, title, id);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}

			return "title updated";
		}
  

		#endregion
	}

	#region Custom Responses

	public class ArtistResponse
	{
		public Artist Artist { get; set; }

		public List<Album> Albums { get; set; }
	}

	#endregion
}

