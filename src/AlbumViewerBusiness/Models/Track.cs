using System.Diagnostics;

namespace AlbumViewerBusiness
{

    [DebuggerDisplay("{SongName}")]
    public class Track
    {
        public int Id { get; set; }
        public int AlbumId { get; set; }                
        public string SongName { get; set; }
        public string Length { get; set; }
        public int Bytes { get; set; }
        public decimal UnitPrice { get; set; }

        public string BuildInsertQuery(Track track) {
            return "INSERT INTO Tracks (Id, AlbumId, SongName, Length, Bytes, UnitPrice) VALUES (NULL, " + track.AlbumId + ", '" + track.SongName + "', '" + track.Length + "', " + track.Bytes + ", '" + track.UnitPrice + "')";
        }

        public override string ToString()
        {
            return SongName;
        }
    }
}