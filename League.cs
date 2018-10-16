﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace Torn
{
	public enum Colour { None = 0, Red, Blue, Green, Yellow, Purple, Pink, Cyan, Orange };
	public static class ColourExtensions
	{
		public static Color ToColor(this Colour colour)
		{
			Color[] Colors = { Color.Empty, Color.FromArgb(0xFF, 0xA0, 0xA0), Color.FromArgb(0xA0, 0xD0, 0xFF), 
				Color.FromArgb(0xA0, 0xFF, 0xA0), Color.FromArgb(0xFF, 0xFF, 0x90), Color.FromArgb(0xC0, 0xA0, 0xFF), 
				Color.FromArgb(0xFF, 0xA0, 0xF0), Color.FromArgb(0xA0, 0xFF, 0xFF), Color.FromArgb(0xFF, 0xD0, 0xA0) };
			return Colors[(int)colour];
		}

		public static Colour ToColour(string s)
		{
			if (string.IsNullOrEmpty(s)) 
				return Colour.None;

			var dict = new Dictionary<string, Colour> { 
				{ "red", Colour.Red }, { "blue", Colour.Blue }, { "green", Colour.Green }, { "yellow", Colour.Yellow },
				{ "purple", Colour.Purple }, { "pink", Colour.Pink }, { "cyan", Colour.Cyan }, { "orange", Colour.Orange },
				{ "blu", Colour.Blue }, { "grn", Colour.Green }, { "yel", Colour.Yellow } 
			};

			Colour c;
			dict.TryGetValue(s.ToLower(CultureInfo.InvariantCulture), out c);
			return c;
		}
	}

	public enum HandicapStyle { Percent, Plus, Minus };
	public static class HandicapExtensions
	{
		public static HandicapStyle ToHandicapStyle(string s)
		{
		    var dict = new Dictionary<string, HandicapStyle> { 
				{ "%", HandicapStyle.Percent }, { "+", HandicapStyle.Plus }, { "-", HandicapStyle.Minus }
			};

			HandicapStyle h;
			dict.TryGetValue(s.ToLower(CultureInfo.InvariantCulture), out h);
			return h;
		}
	}

	public class Handicap
	{
		public double? Value { get; set; }
		public HandicapStyle Style { get; set; }

		public Handicap() {}

		public Handicap(double? value, HandicapStyle style)
		{
			Value = value;
			Style = style;
		}

		public double Apply(double score)
		{
			return Value == null ? score :
				Style == HandicapStyle.Percent ? score * (double)Value / 100 :
				Style == HandicapStyle.Plus ?    score + (double)Value :
				                                 score - (double)Value;
		}

		/// <summary>Parse strings like "110%", "+1000", "-1000", "110" into handicap value and style.</summary>
		public static Handicap Parse(string s)
		{
			var h = new Handicap();

			if (!string.IsNullOrEmpty(s))
			{
				if (s[s.Length - 1] == '%')
				{
					h.Value = int.Parse(s.Substring(0, s.Length - 2), CultureInfo.InvariantCulture);
					h.Style = HandicapStyle.Percent;
				}
				else if (s[0] == '+')
				{
					h.Value = int.Parse(s.Substring(1), CultureInfo.InvariantCulture);
					h.Style = HandicapStyle.Plus;
				}
				else if (s[0] == '-')
				{
					h.Value = int.Parse(s.Substring(1), CultureInfo.InvariantCulture);
					h.Style = HandicapStyle.Minus;
				}
				else  // No style specified -- assume '%'.
				{
					h.Value = int.Parse(s, CultureInfo.InvariantCulture);
					h.Style = HandicapStyle.Percent;
				}
			}

			return h;
		}

		public override string ToString()
		{
			return Value == null ? "Handicap" :
				(Style == HandicapStyle.Percent) ? ((double)Value).ToString(CultureInfo.InvariantCulture) + '%' :
				Style.ToString() + ((double)Value).ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>True if the handicap is 100%, +0 or -0.</summary>
		public bool IsZero()
		{
			return (Style == HandicapStyle.Percent && Value == 100) || (Style != HandicapStyle.Percent && Value == 0);
		}
	}

	public class LeaguePlayer
	{
		/// <summary>Friendly name e.g. "RONiN".</summary>
		public string Name { get; set; }
		/// <summary>Under-the-hood laser game system identifier e.g. "P11-JP9", "1-50-50", etc.</summary>
		public string Id { get; set; }  // 
		/// <summary>Represents player handicap +/-/%.</summary>
		public Handicap Handicap { get; set; }
		/// <summary>User-defined. Often used for player grade.</summary>
		public string Comment { get; set; }  // 
	
		Collection<GamePlayer> played;
		/// <summary>Games this player has played in. Set by LinkThings().</summary>
		public Collection<GamePlayer> Played { get { return played; } }

		public LeaguePlayer() 
		{
			played = new Collection<GamePlayer>();
		}

		public LeaguePlayer Clone()
		{
			LeaguePlayer clone = new LeaguePlayer();
			clone.Name = Name;
			clone.Id = Id;
			clone.Handicap = Handicap;
			clone.Comment = Comment;

			//clone.played = new Collection<GamePlayer>(played);

			return clone;
		}

		public override string ToString()
		{
			return Name;
		}
	}

	/// <summary>Stores data about each remembered league team</summary>
	public class LeagueTeam: IComparable
	{
		string name;
		public string Name 
		{
			get { 
				if (string.IsNullOrEmpty(name))
				{
					if (Players.Count == 0)
						name = "Team " + Id.ToString(CultureInfo.InvariantCulture);
					else if (Players.Count == 2)
						name = Players[0].Name + " and " + Players[1].Name;
					else
						name = Players[0].Name + "'s team";
				}
				return name;
			}
			
			set { name = value; } 
		}
		
		internal int Id  { get; set; }
		public Handicap Handicap { get; set; }
		public string Comment { get; set; }
		public List<LeaguePlayer> Players { get; private set; }
		public List<GameTeam> AllPlayed { get; private set; }

		public LeagueTeam()
		{
			Players = new List<LeaguePlayer>();
			AllPlayed = new List<GameTeam>();
		}
		
		public List<GameTeam> Played(bool includeSecret)
		{
			return includeSecret ? AllPlayed : AllPlayed.FindAll(x => !x.Game.Secret);
		}
		
		public double AverageScore(bool includeSecret)
		{
			return Played(includeSecret).Average(x => x.Score);
		}

		public double AveragePoints(bool includeSecret)
		{
			return Played(includeSecret).Average(x => x.Points);
		}

		public LeagueTeam Clone()
		{
			var clone = new LeagueTeam();
			clone.Name = Name;
			clone.Id = Id;
			clone.Handicap = Handicap;
			clone.Comment = Comment;
			
			clone.Players = new List<LeaguePlayer>(Players);
			//clone.AllPlayed = new List<GameTeam>(AllPlayed);
			
			return clone;
		}

		int IComparable.CompareTo(object obj)
		{
			return string.Compare(this.Name, ((LeagueTeam)obj).Name);
		}

		public override string ToString()
		{
			return Name;
		}
	}

	public class GameTeam: IComparable
	{
		public int? TeamId { get; set; }
		public LeagueTeam LeagueTeam { get; set; }
		public Game Game { get; set; }
		Colour colour;
		public Colour Colour
		{
			get
			{
				if (colour == Colour.None)
				{
					var counts = new int[9];
					foreach (var player in players)
						counts[(int)player.Colour]++;

					colour = (Colour)counts.ToList().IndexOf(counts.Max());
				}
				return colour;
			}
			set { colour = value; }
		}
		public int Score { get; set; }
		public int Adjustment { get; set; }
		public int Points { get; set; }
		public int PointsAdjustment { get; set; }

		List<GamePlayer> players;
		public List<GamePlayer> Players { get { return players; } }

		public GameTeam()
		{
			players = new List<GamePlayer>();
		}

		public double CalculateScore(HandicapStyle handicapStyle)
		{
			double score = 0;

			foreach (var player in players)
				score += player.Score;

			score += Adjustment;

			return LeagueTeam != null && LeagueTeam.Handicap != null ? new Handicap(LeagueTeam.Handicap.Value, handicapStyle).Apply(score) : score;
		}

		public GameTeam Clone()
		{
			var clone = new GameTeam();
			clone.TeamId = TeamId;
			clone.Colour = colour;
			clone.Score = Score;
			clone.Adjustment = Adjustment;
			clone.Points = Points;
			clone.PointsAdjustment = PointsAdjustment;
			// Don't clone LeagueTeam, Game, or players as these will be done by LinkThings().

			return clone;
		}

		int IComparable.CompareTo(object obj)
		{
			GameTeam gt = (GameTeam)obj;
			if (this.Points == gt.Points)
				return gt.Score - this.Score;
			else
				return gt.Points - this.Points;
		}

		public override string ToString()
		{
			if (LeagueTeam == null)
				return (TeamId ?? -1).ToString();
			else
				return LeagueTeam.Name;

//			return "GameTeam " + LeagueTeam == null ? (TeamId ?? -1).ToString() : LeagueTeam.Name;
		}
	}

	public class GamePlayer: IComparable
	{
		public int GameTeamId { get; set; }
		public GameTeam GameTeam { get; set; }
		public LeaguePlayer LeaguePlayer { get; set; }
		/// <summary>Under-the-hood laser game system identifier e.g. "P11-JP9", "1-50-50", etc. Same as LeaguePlayer.Id.</summary>
		public string PlayerId { get; set; }
		public string Pack { get; set; }
		public int Score { get; set; }
		public uint Rank { get; set; }
		public Colour Colour { get; set; }
		public int HitsBy { get; set; }
		public int HitsOn { get; set; }
		public int BaseHits { get; set; }
		public int BaseDestroys { get; set; }
		public int BaseDenies { get; set; }
		public int BaseDenied { get; set; }
		public int YellowCards { get; set; }
		public int RedCards { get; set; }
		public Game Game { get; set; }

		public GamePlayer CopyTo(GamePlayer target)
		{
			target.GameTeamId = GameTeamId;
			target.GameTeam = GameTeam;
			target.LeaguePlayer = LeaguePlayer;
			target.PlayerId = PlayerId;
			target.Pack = Pack;
			target.Score = Score;
			target.Rank = Rank;
			target.HitsBy = HitsBy;
			target.HitsOn = HitsOn;
			target.BaseHits = BaseHits;
			target.BaseDestroys = BaseDestroys;
			target.Colour = Colour;
			target.Game = Game;
			
			return target;
		}

		int IComparable.CompareTo(object obj)
		{
			return ((GamePlayer)obj).Score - this.Score;
		}

		public override string ToString()
		{
			return LeaguePlayer == null ? "Player " + PlayerId.ToString() : LeaguePlayer.Name;
		}
	}

	public class Game: IComparable
	{
		public string Title { get; set; }
		public DateTime Time { get; set; }
		public bool Secret { get; set; }  // If true, don't serve this game from our internal webserver, or include it in any webserver reports.
		public List<GameTeam> Teams { get; private set; }
		public List<GamePlayer> Players { get; private set; }

		int? hits = null;
		public int Hits { get 
			{
				if (hits == null)
					hits = Players.Sum(p => p.HitsOn);

				return (int)hits;
			} 
		}

		public Game()
		{
			Teams = new List<GameTeam>();
			Players = new List<GamePlayer>();
		}

		int IComparable.CompareTo(object obj)
		{
		   Game g = (Game)obj;
		   return DateTime.Compare(this.Time, g.Time);
		}

		public override string ToString()
		{
			return Secret ? string.Join(", ", this.Teams.OrderBy(x => x.ToString()).Select(x => x.ToString())) :
				string.Join(", ", this.Teams.Select(x => x.ToString()));
		}

		int? totalScore = null;
		public int TotalScore()
		{
			if (totalScore == null)
				totalScore = Teams.Sum(t => t.Score);

			return (int)totalScore;
		}
	}

	public class Games: List<Game>
	{
		public DateTime? MostRecent()
		{
			return (this.Count == 0) ? (DateTime?)null : this.Select(x => x.Time).Max();
		}
	}

	public class GameTeamData
	{
		public GameTeam GameTeam { get; set; }
		public List<ServerPlayer> Players { get; set; }
	}

	/// <summary>Load and manage a .Torn league file, containing league teams and games.</summary>
	public class League
	{
		public string Title { get; set; }
		string file;

		public int GridHigh { get; set; }
		public int GridWide { get; set; }
		public int GridPlayers { get; set; }
		int sortMode, sortByRank, autoUpdate, updateTeams, elimMultiplier;
		public HandicapStyle HandicapStyle { get; set; }

		List<LeagueTeam> teams;
		public List<LeagueTeam> Teams { get { return teams; } }

		List<LeaguePlayer> players;
		public List<LeaguePlayer> Players { get { return players; } }

		public Games AllGames { get; private set; }

		Collection<double> victoryPoints;
		public Collection<double> VictoryPoints { get { return victoryPoints; } }
		
		public double VictoryPointsHighScore { get; set; }
		public double VictoryPointsProportional { get; set; }

		public League()
		{
			teams = new List<LeagueTeam>();
			players = new List<LeaguePlayer>();
			AllGames = new Games();
			victoryPoints = new Collection<double>();
			
			GridHigh = 3;
			GridWide = 1;
			GridPlayers = 6;
			HandicapStyle = HandicapStyle.Percent;
		}

		void Clear()
		{
			Title = "";
			teams.Clear();
			players.Clear();
			AllGames.Clear();
			victoryPoints.Clear();
			VictoryPointsHighScore = 0;
		}

		/// <summary>This is a deep clone of league teams, league players and victory points, but a shallow clone of game data: Game, GameTeam, GamePlayer.</summary>
		public League Clone()
		{
			League clone = new League();

			clone.Title = Title;
			clone.file = file;

			clone.GridHigh = GridHigh;
			clone.GridWide = GridWide;
			clone.GridPlayers = GridPlayers;
			
			clone.teams = Teams.Select(item => (LeagueTeam)item.Clone()).ToList();
			clone.players = Players.Select(item => (LeaguePlayer)item.Clone()).ToList();
			clone.AllGames.AddRange(AllGames);
			
			clone.victoryPoints = new Collection<double>(VictoryPoints.Select(item => item).ToList());
			clone.VictoryPointsHighScore = VictoryPointsHighScore;
			clone.VictoryPointsProportional = VictoryPointsProportional;
			
			clone.LinkThings();

			return clone;
		}

		void LinkPlayerToGame(GamePlayer gamePlayer, GameTeam gameTeam, ServerGame game)
		{
			gamePlayer.GameTeam = gameTeam;

			if (!game.Game.Players.Contains(gamePlayer))
				game.Game.Players.Add(gamePlayer);

			if (gamePlayer.LeaguePlayer == null)
				gamePlayer.LeaguePlayer = Players.Find(p => p.Id == gamePlayer.PlayerId);

			if (gamePlayer.LeaguePlayer == null)
			{
				gamePlayer.LeaguePlayer = new LeaguePlayer();
				if (gamePlayer is ServerPlayer)
					gamePlayer.LeaguePlayer.Name = ((ServerPlayer)gamePlayer).Alias;
				gamePlayer.LeaguePlayer.Id = gamePlayer.PlayerId;
				Players.Add(gamePlayer.LeaguePlayer);
			}

			if (!gameTeam.LeagueTeam.Players.Contains(gamePlayer.LeaguePlayer))
				gameTeam.LeagueTeam.Players.Add(gamePlayer.LeaguePlayer);

			if (!gamePlayer.LeaguePlayer.Played.Contains(gamePlayer))
				gamePlayer.LeaguePlayer.Played.Add(gamePlayer);
		}

		void LinkTeamToGame(GameTeamData teamData, ServerGame serverGame, LeagueTeam leagueTeam)
		{
			if (teamData.GameTeam == null)
				teamData.GameTeam = new GameTeam();

			var gameTeam = teamData.GameTeam;
			gameTeam.Game = serverGame.Game;

			gameTeam.Game.Teams.Add(gameTeam);

			gameTeam.LeagueTeam = leagueTeam ?? GuessTeam(gameTeam.Players.Select(x => x.PlayerId).ToList());
			
			if (gameTeam.LeagueTeam == null)
			{
				gameTeam.LeagueTeam = new LeagueTeam();
				gameTeam.LeagueTeam.Id = NextTeamID();
				Teams.Add(gameTeam.LeagueTeam);
			}
			
			gameTeam.TeamId = gameTeam.LeagueTeam.Id;

			gameTeam.LeagueTeam.AllPlayed.RemoveAll(x => x.Game.Time == gameTeam.Game.Time);
			gameTeam.LeagueTeam.AllPlayed.Add(gameTeam);
		}

		public void CommitGame(ServerGame serverGame, List<GameTeamData> teamDatas)
		{
			if (serverGame.Game == null)
				serverGame.Game = new Game();

			serverGame.Game.Time = serverGame.Time;
			serverGame.Game.Teams.Clear();
			foreach (var teamData in teamDatas)
			{
//				var gameTeam = serverGame.Game.Teams.Find(gt => gt.LeagueTeam == teamData.GameTeam.LeagueTeam);
				LinkTeamToGame(teamData, serverGame, teamData.GameTeam.LeagueTeam);

				foreach (var player in teamData.Players)
					LinkPlayerToGame(serverGame.Game.Players.Find(gp => gp.PlayerId == player.PlayerId) ?? player, //.CopyTo(new GamePlayer()),
					                 teamData.GameTeam, serverGame);

				teamData.GameTeam.Players.Sort();
				teamData.GameTeam.Score = (int)teamData.GameTeam.CalculateScore(HandicapStyle);
			}

			serverGame.Game.Players.Sort();
			for (int i = 0; i < serverGame.Game.Players.Count; i++)
				serverGame.Game.Players[i].Rank = (uint)i + 1;

			if (!AllGames.Contains(serverGame.Game))
				AllGames.Add(serverGame.Game);

			serverGame.Game.Teams.Sort();
		}

		public Games Games(bool includeSecret)
		{
			if (includeSecret) 
				return AllGames;

			var publicGames = new Games();
			publicGames.AddRange(AllGames.Where(g => !g.Secret).ToList());
			return publicGames;
		}
		
		public List<LeagueTeam> GuessTeams(ServerGame game)
		{
			List<LeagueTeam> Result = new List<LeagueTeam>();

			var teams = game.Players.Select(x => x.PandCPlayerTeamId).Distinct();

			foreach (int teamId in teams)
				Result.Add(GuessTeam(game.Players.FindAll(x => x.PandCPlayerTeamId == teamId).Select(y => y.PlayerId).ToList()));

			return Result;
		}

		public LeagueTeam GuessTeam(List<string> ids)
		{
			if (teams.Count == 0 || ids.Count == 0)
				return null;

			LeagueTeam bestTeam = null;
			double bestScore = 0;

			foreach (LeagueTeam team in teams)
				if (team.Players.Count > 0)
				{
					double thisScore = 1.0 * team.Players.FindAll(p => ids.Contains(p.Id)).Count / team.Players.Count;

					if (bestScore < thisScore)
					{
						bestScore = thisScore;
						bestTeam = team;
					}
				}

			return bestTeam;
		}

		public bool IsPoints()
		{
			foreach (Game game in AllGames)
				foreach (GameTeam gameTeam in game.Teams)
					if (gameTeam.Points != 0)
						return true;
			
			return false;
		}

		int NextTeamID()
		{
			return teams.Count == 0 ? 0 : teams.Max(x => x.Id) + 1;
		}

		static string XmlValue(XmlNode node, string name)
		{
			var child = node.SelectSingleNode(name);
			return child == null ? null : child.InnerText;
		}

		static double XmlDouble(XmlNode node, string name)
		{
			var child = node.SelectSingleNode(name);
			return child == null ? 0.0 : double.Parse(child.InnerText, CultureInfo.InvariantCulture);
		}

		static int XmlInt(XmlNode node, string name)
		{
			var child = node.SelectSingleNode(name);
			return child == null ? 0 : int.Parse(child.InnerText, CultureInfo.InvariantCulture);
		}

		/// <summary>Load a .Torn file from disk.</summary>
		public void Load(string fileName)
		{
			Clear();

			Title = Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ');

			var doc = new XmlDocument();
			doc.Load(fileName);

			var root = doc.DocumentElement;

			GridHigh = XmlInt(root, "GridHigh");
			GridWide = XmlInt(root, "GridWide");
			GridPlayers = XmlInt(root, "GridPlayers");
			sortMode = XmlInt(root, "SortMode");
			string s = XmlValue(root, "HandicapStyle");
			HandicapStyle = s == "%" ? HandicapStyle.Percent : s == "+" ? HandicapStyle.Plus : HandicapStyle.Minus;
			sortByRank = XmlInt(root, "SortByRank");
			autoUpdate = XmlInt(root, "AutoUpdate");
			updateTeams = XmlInt(root, "UpdateTeams");
			elimMultiplier = XmlInt(root, "ElimMultiplier");

			XmlNodeList points = root.SelectNodes("Points");
			foreach (XmlNode point in points)
				victoryPoints.Add(double.Parse(point.InnerText, CultureInfo.InvariantCulture));

			VictoryPointsHighScore = XmlDouble(root, "High");
			VictoryPointsProportional = XmlDouble(root, "Proportional");

			XmlNodeList xteams = root.SelectSingleNode("leaguelist").SelectNodes("team");

			foreach (XmlNode xteam in xteams)
			{
				LeagueTeam leagueTeam = new LeagueTeam();
				
				leagueTeam.Name = XmlValue(xteam, "teamname");
				leagueTeam.Id = XmlInt(xteam, "teamid");
				leagueTeam.Handicap = Handicap.Parse(XmlValue(xteam, "handicap"));
				leagueTeam.Comment = XmlValue(xteam, "comment");
				
				var teamPlayers = xteam.SelectSingleNode("players");
				if (teamPlayers != null)
				{
					XmlNodeList xplayers = teamPlayers.SelectNodes("player");
					
					foreach (XmlNode xplayer in xplayers)
					{
						LeaguePlayer leaguePlayer;
						string id = XmlValue(xplayer, "buttonid");;
						
						leaguePlayer = players.Find(x => x.Id == id);
						if (leaguePlayer == null)
						{
							leaguePlayer = new LeaguePlayer();
							players.Add(leaguePlayer);
							leaguePlayer.Id = id;
						}

						leaguePlayer.Name = XmlValue(xplayer, "name");
						leaguePlayer.Handicap = Handicap.Parse(XmlValue(xteam, "handicap"));
						leaguePlayer.Comment = XmlValue(xplayer, "comment");

						if (!leagueTeam.Players.Exists(x => x.Id == id))
							leagueTeam.Players.Add(leaguePlayer);
					}
				}

				teams.Add(leagueTeam);
			}

			XmlNodeList xgames = root.SelectSingleNode("games").SelectNodes("game");

			foreach (XmlNode xgame in xgames) 
			{
				Game game = new Game();

				game.Title = XmlValue(xgame, "title");
				game.Secret = XmlValue(xgame, "secret") == "true";

				var child = xgame.SelectSingleNode("ansigametime");
				if (child == null)
					game.Time = new DateTime(1899, 12, 30).AddDays(double.Parse(XmlValue(xgame, "gametime"), CultureInfo.InvariantCulture));
				else
					game.Time = DateTime.Parse(child.InnerText, CultureInfo.InvariantCulture);
				//game.Hits = XmlInt(xgame, "hits");

				xteams = xgame.SelectSingleNode("teams").SelectNodes("team");

				foreach (XmlNode xteam in xteams)
				{
					GameTeam gameTeam = new GameTeam();

					gameTeam.TeamId = XmlInt(xteam, "teamid");
					gameTeam.Game = game;
					gameTeam.Colour = ColourExtensions.ToColour(XmlValue(xteam, "colour"));
					gameTeam.Score = XmlInt(xteam, "score");
					gameTeam.Points = XmlInt(xteam, "points");
					gameTeam.Adjustment = XmlInt(xteam, "adjustment");
					gameTeam.PointsAdjustment = XmlInt(xteam, "victorypointsadjustment");

					game.Teams.Add(gameTeam);
				}
				game.Teams.Sort();

				XmlNodeList xplayers = xgame.SelectSingleNode("players").SelectNodes("player");

				foreach (XmlNode xplayer in xplayers)
				{
					GamePlayer gamePlayer = new GamePlayer();

					gamePlayer.PlayerId = XmlValue(xplayer, "playerid");
					gamePlayer.GameTeamId = XmlInt(xplayer, "teamid");
					gamePlayer.Pack = XmlValue(xplayer, "pack");
					gamePlayer.Score = XmlInt(xplayer, "score");
					gamePlayer.Rank = (uint)XmlInt(xplayer, "rank");
					gamePlayer.HitsBy = XmlInt(xplayer, "hitsby");
					gamePlayer.HitsOn = XmlInt(xplayer, "hitson");

					if (xplayer.SelectSingleNode("colour") != null)
						gamePlayer.Colour = (Colour)(XmlInt(xplayer, "colour") + 1);

					game.Players.Add(gamePlayer);
				}
				
				AllGames.Add(game);
			}

			AllGames.Sort();
			LinkThings();

			file = fileName;
		}

		/// <summary>
		/// If a league has just been loaded, it has a bunch of "links" present only as ID fields.
		/// LinkThings() matches these, and populates links and lists accordingly. 
		/// </summary>
		void LinkThings()
		{
			teams.Sort();
			foreach (var leagueTeam in teams)
			{
				leagueTeam.AllPlayed.Clear();
				
				foreach (var player in leagueTeam.Players)
					player.Played.Clear();
			}

			foreach (Game game in AllGames)
			{
				foreach (GameTeam gameTeam in game.Teams) 
				{
					// Connect each game team back to their league team.
					gameTeam.LeagueTeam = teams.Find(x => x.Id == gameTeam.TeamId);
					if (gameTeam.LeagueTeam != null)
					{
						gameTeam.LeagueTeam.AllPlayed.Add(gameTeam);

						// Connect each game player to their game team.
						gameTeam.Players.Clear();
						foreach (GamePlayer gamePlayer in game.Players)
							if (gamePlayer.GameTeamId == gameTeam.TeamId)
						{
								gameTeam.Players.Add(gamePlayer);
								gamePlayer.GameTeam = gameTeam;
						}

						// Connect each game player to their league player.
						foreach (GamePlayer gamePlayer in gameTeam.Players)
							foreach (LeaguePlayer leaguePlayer in gameTeam.LeagueTeam.Players) 
								if (gamePlayer.PlayerId == leaguePlayer.Id)
								{
									gamePlayer.LeaguePlayer = leaguePlayer;
									leaguePlayer.Played.Add(gamePlayer);
								}
					}
					
					foreach (var player in game.Players)
						player.Game = game;
				}
			}
		}

		public void New(string fileName)
		{
			Clear();

			Title = Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ');
		}

		void AppendNode(XmlDocument doc, XmlNode parent, string name, string value)
		{
			if (!string.IsNullOrEmpty(value))
			{
				XmlNode node = doc.CreateElement(name);
				node.AppendChild(doc.CreateTextNode(value));
				parent.AppendChild(node);
			}
		}

		void AppendNode(XmlDocument doc, XmlNode parent, string name, int value)
		{
			AppendNode(doc, parent, name, value.ToString());
		}

		void AppendNonZero(XmlDocument doc, XmlNode parent, string name, int value)
		{
			if (value != 0)
				AppendNode(doc, parent, name, value.ToString());
		}

		/// <summary>Save a .Torn file to disk.</summary>
		public void Save(string fileName = "")
		{
			if (!string.IsNullOrEmpty(fileName))
				file = fileName;

			XmlDocument doc = new XmlDocument();
			XmlNode docNode = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
			doc.AppendChild(docNode);

			XmlNode bodyNode = doc.CreateElement("body");
			doc.AppendChild(bodyNode);

			AppendNode(doc, bodyNode, "GridHigh", GridHigh);
			AppendNode(doc, bodyNode, "GridWide", GridWide);
			AppendNode(doc, bodyNode, "GridPlayers", GridPlayers);
			AppendNode(doc, bodyNode, "SortMode", sortMode);
			AppendNode(doc, bodyNode, "HandicapStyle", HandicapStyle.ToString());
			AppendNonZero(doc, bodyNode, "SortByRank", sortByRank);
			AppendNode(doc, bodyNode, "AutoUpdate", autoUpdate);
			AppendNode(doc, bodyNode, "UpdateTeams", updateTeams);
			AppendNonZero(doc, bodyNode, "ElimMultiplier", elimMultiplier);

			foreach (double point in victoryPoints)
				AppendNode(doc, bodyNode, "Points", point.ToString());

			if (VictoryPointsHighScore != 0) AppendNode(doc, bodyNode, "High", VictoryPointsHighScore.ToString());
			if (VictoryPointsProportional != 0) AppendNode(doc, bodyNode, "Proportional", VictoryPointsProportional.ToString());

			XmlNode leagueTeamsNode = doc.CreateElement("leaguelist");
			bodyNode.AppendChild(leagueTeamsNode);

			foreach (var team in teams)
			{
				XmlNode teamNode = doc.CreateElement("team");
				leagueTeamsNode.AppendChild(teamNode);

				AppendNode(doc, teamNode, "teamname", team.Name);
				AppendNode(doc, teamNode, "teamid", team.Id);
				if (team.Handicap != null && !team.Handicap.IsZero())
					AppendNode(doc, teamNode, "handicap", team.Handicap.ToString());
				if (!string.IsNullOrEmpty(team.Comment)) AppendNode(doc, teamNode, "comment", team.Comment);

				XmlNode playersNode = doc.CreateElement("players");
				teamNode.AppendChild(playersNode);

				foreach (var player in team.Players)
				{
					XmlNode playerNode = doc.CreateElement("player");
					playersNode.AppendChild(playerNode);

					AppendNode(doc, playerNode, "name", player.Name);
					AppendNode(doc, playerNode, "buttonid", player.Id);
					if (player.Handicap != null && !player.Handicap.IsZero())
						AppendNode(doc, playerNode, "handicap", player.Handicap.ToString());
					if (!string.IsNullOrEmpty(player.Comment)) AppendNode(doc, playerNode, "comment", player.Comment);
				}
			}

			XmlNode gamesNode = doc.CreateElement("games");
			bodyNode.AppendChild(gamesNode);

			foreach (var game in AllGames)
			{
				XmlNode gameNode = doc.CreateElement("game");
				gamesNode.AppendChild(gameNode);

				AppendNode(doc, gameNode, "title", game.Title);
				AppendNode(doc, gameNode, "ansigametime", game.Time.ToString("yyyy/MM/dd HH:mm:ss"));
				AppendNode(doc, gameNode, "hits", game.Hits);
				if (game.Secret)
					AppendNode(doc, gameNode, "secret", "y");

				XmlNode teamsNode = doc.CreateElement("teams");
				gameNode.AppendChild(teamsNode);

				foreach (var team in game.Teams)
				{
					XmlNode teamNode = doc.CreateElement("team");
					teamsNode.AppendChild(teamNode);

					AppendNode(doc, teamNode, "teamid", team.TeamId ?? -1);
					AppendNode(doc, teamNode, "colour", team.Colour.ToString());
					AppendNode(doc, teamNode, "score", team.Score);
					AppendNonZero(doc, teamNode, "points", team.Points);
					AppendNonZero(doc, teamNode, "adjustment", team.Adjustment);
					AppendNonZero(doc, teamNode, "victorypointsadjustment", team.PointsAdjustment);
				}

				XmlNode playersNode = doc.CreateElement("players");
				gameNode.AppendChild(playersNode);

				foreach (var player in game.Players)
				{
					XmlNode playerNode = doc.CreateElement("player");
					playersNode.AppendChild(playerNode);

					AppendNode(doc, playerNode, "teamid", player.GameTeamId);
					AppendNode(doc, playerNode, "playerid", player.PlayerId);
					AppendNode(doc, playerNode, "pack", player.Pack);
					AppendNode(doc, playerNode, "score", player.Score);
					AppendNode(doc, playerNode, "rank", player.Rank.ToString());
					AppendNonZero(doc, playerNode, "hitsby", player.HitsBy);
					AppendNonZero(doc, playerNode, "hitson", player.HitsOn);
					AppendNonZero(doc, playerNode, "basehits", player.BaseHits);
					AppendNonZero(doc, playerNode, "basedestroys", player.BaseDestroys);

					AppendNode(doc, playerNode, "colour", (int)player.Colour);
				}
			}
			doc.Save(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + "5") + Path.GetExtension(file));
		}

		public override string ToString()
		{
			return Title ?? "league with blank title";
		}
	}

	/// <summary>Represents a game as stored on the laser game server.</summary>
	public class ServerGame: IComparable<ServerGame>
	{
		public int GameId { get; set; }
		public string Description { get; set; }
		public DateTime Time { get; set; }
		public League League { get; set; }
		public Game Game { get; set; }
		public List<ServerPlayer> Players { get; set; }
		public bool InProgress { get; set; }
		public bool OnServer { get; set; }
//		public List<Event> Events { get; set; }
		
		public ServerGame()
		{
//			Events = new List<Event>();
		}

		public int CompareTo(ServerGame compareGame)
	    {
			return compareGame == null ? 1 : this.Time.CompareTo(compareGame.Time);
	    }
	}

	/// <summary>Represents a player as stored on the laser game server.</summary>
	public class ServerPlayer: GamePlayer
	{
		public int PandCPlayerId { get; set; }  // This is the under-the-hood PAndC table ID field.
		public int PandCPlayerTeamId { get; set; }  // Ditto. These two are only used by systems with in-game data available.
		public string PackName { get; set; }
		public string Alias { get; set; }
		/// <summary>If this object is linked from a ListViewItem's Tag, list that ListViewItem here.</summary>
		public ListViewItem Item { get; set; }
	}
}
