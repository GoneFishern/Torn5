﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Torn
{
	/// <summary>
	/// This represents a Laserforce lasergame database server.
	/// You can ask it for scores for players, but not for individual events in game, nor time remaining.
	/// </summary>
	public class Laserforce: LaserGameServer
	{
		SqlConnection connection;
		//public bool Connected { get; private set; }
		public int GamesLimit;

		public Laserforce()
		{
			GamesLimit = 1000;
			
			string server = "lf-main\\lf6";
			try
			{
				connection = new SqlConnection("Data Source=" + server + 
					";Initial Catalog=Laserforce;User ID=UserName;Password=Password;Database=Laserforce;Trusted_Connection=True");
				connection.Open();
				Connected = true;
			}
			catch
			{
				Connected = false;
				throw;
			}
		}

		public override void Dispose()
		{
			if (Connected)
			{
				connection.Close();
				Connected = false;
			}
		}

		public override List<ServerGame> GetGames()
		{
			List<ServerGame> games = new List<ServerGame>();

			if (!Connected)
				return games;

			string sql = "SELECT TOP " + GamesLimit.ToString() + 
						 " M.ref AS [Game_ID], M.start AS [Start_Time], COALESCE(MT.desc1, MT.desc0, MG.[desc]) AS [Description] " +
						 "FROM Mission M " +
						 "LEFT JOIN MissionGroup MG ON MG.ref = M.[group] " +
						 "LEFT JOIN MissionType MT ON MT.ref = M.[type] " +
						 "ORDER BY M.start DESC";
			var cmd = new SqlCommand(sql, connection);
			SqlDataReader reader = cmd.ExecuteReader();

			try
			{
				while (reader.Read())
				{
					ServerGame game = new ServerGame();
					game.GameId = reader.GetInt32(0);
					game.Description = reader.GetString(2);
					game.Time = reader.GetDateTime(1);
					game.OnServer = true;
					games.Add(game);
				}
			}
			finally
			{
				reader.Close();
			}
			return games;
		}

		public override void PopulateGame(ServerGame game)
		{
			if (!Connected)
				return;

			game.Players = new List<ServerPlayer>();

			string sql = "SELECT MAT.colourTeam AS [Colour], MP.score, U.[desc] AS [Pack_Name], " +
						 "cast(C.region as varchar) + '-' + cast(C.site as varchar) + '-' + cast(Member.id as varchar) as [Player_ID], " +
						 "member.codename AS [Alias], MAT.[desc] AS [Team] " +
						 "FROM MissionPlayer MP " +
						 "LEFT JOIN Unit U ON U.ref = MP.unit " +
						 "LEFT JOIN Member ON Member.ref = MP.member " +
						 "LEFT JOIN Centre C ON C.ref = Member.centre " +
						 "LEFT JOIN Mission ON Mission.ref = MP.mission " +
						 "LEFT JOIN MissionType MT ON MT.ref = Mission.type " +
						 "LEFT JOIN MissionAlignmentTeam MAT ON MAT.alignment = MT.alignment AND MAT.seq = MP.team " +
						 "WHERE Mission.ref = " + game.GameId.ToString() + " AND unitType = 0 " +
						 "ORDER BY score DESC";
			var cmd = new SqlCommand(sql, connection);
			SqlDataReader reader = cmd.ExecuteReader();

			try
			{
				while (reader.Read())
				{
					ServerPlayer player = new ServerPlayer();
					//player.PlayerId = reader.GetInt32(3);
					//player.PlayerTeamId = reader.GetInt32(5);
					player.Score = reader.GetInt32(1);
					player.PackName = reader.GetString(2);
					player.PlayerId = reader.GetString(3);
					player.Alias = reader.GetString(4);
					game.Players.Add(player);
				}
			}
			finally
			{
				reader.Close();
			}
		}
	}
}