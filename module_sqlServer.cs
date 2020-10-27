using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;

namespace DRS.App1
{
	/* 基本的にエラーの場合は全て例外を飛ばす
	 * ArgumentException         : 使用者が正しい(想定内)操作をすれば回避できるもの
	 * InvalidOperationException : 上記以外
	 */
	public class SqlServer
	{
		/* プロパティ */
		public int ColumnCount {                                    //createInfoしたテーブルの列数
			get { return this._colInfo.Rows.Count; }
		}

		/* Local */
		protected string _connectionString = "";            //DB接続文字列
		protected string _tableName;                        //createInfoしたテーブル名
		protected DataTable _colInfo = null;                //項目情報

		protected StringBuilder sbSQLCmd = new StringBuilder(); //クエリ

		//項目のColumnOrdinal(ハッシュテーブル)
		protected Dictionary<string, int> gColOrd = new Dictionary<string, int>();

		/*---------------------------------------------------------------------
		 * DESCRIPTION ：コンストラクタ
		 * INPUT       ：web.configの接続名
		 *---------------------------------------------------------------------*/
		public SqlServer(string conString = "LocalSqlServer")
		{
			if ( ConfigurationManager.ConnectionStrings[conString] == null ) {
				throw new InvalidOperationException("接続名が見つかりません:" + conString);
			}

			this._connectionString = ConfigurationManager.ConnectionStrings[conString].ConnectionString;
		}

		/*---------------------------------------------------------------------
		 * DESCRIPTION ：static
		 *---------------------------------------------------------------------*/
		/*-----------------------------------------------------------------------
		 * NAME        : SQLfromFile
		 * DESCRIPTION : ファイルの中身をargSQLへ戻す
		 * INPUT/OUT   : argSQL1      : フルパスなファイル名
		 *               argSQL2      : フルパスなファイル名
		 *                  argSQL1が存在しない場合、argSQL2を処理する
		 *               argSQL(OUT)  : ファイルの中身(SQL)
		 *               ParamArray   : SQLの引数(可変引数)
		 *                                最初から @p1 を指定値に置き換える
		 * RETURN      : SQL用に変換した値
		 *               null - 失敗
		 * NOTE        :
		 * ATTENTION   : 数値や日付でエラーのものはNULLで登録する
		 * EXAMPLE     : SQLfromFile("argSQLname.sql", argSQL, {"aaa", "bbb"})
		 *                 argSQLname.sqlの中身が
		 *                   SELECT * FROM @p1 WHERE index = '@p2'
		 *                              ↓
		 *                   SELECT * FROM aaa WHERE index = 'bbb'
		 *                 に置き換える
		 * CREATION    : 2006/07/26
		 * HISTORY     :
		 *-----------------------------------------------------------------------*/
		public static string SQLfromFile(string argSQL1, string argSQL2 = "", params object[] argParam)
		{
			string targetSQL = argSQL1;

			if ( !File.Exists(targetSQL) ) {
				targetSQL = argSQL2;

				if ( !File.Exists(targetSQL) ) {
					throw new FileNotFoundException("ファイルが存在しません");
				}
			}

			StringBuilder sbSQL = new StringBuilder();
			string cLine;

			using ( StreamReader osr = new StreamReader(targetSQL) ) {
				while ( !osr.EndOfStream ) {
					cLine = osr.ReadLine().Trim();
					if ( string.IsNullOrWhiteSpace(cLine) || cLine.StartsWith("--") ) {
						continue;
					}

					sbSQL.Append(cLine).Append(" ");
				}
			}

			cLine = sbSQL.ToString().ToUpper();

			if ( string.IsNullOrWhiteSpace(cLine) ) {
				throw new InvalidOperationException("クエリがありません");
			}

			if ( cLine.Contains("DELETE") ) {
				throw new InvalidOperationException("DELETEが含まれています");
			}
			if ( cLine.Contains("INSERT") ) {
				throw new InvalidOperationException("INSERTが含まれています");
			}
			if ( cLine.Contains("UPDATE") ) {
				throw new InvalidOperationException("UPDATEが含まれています");
			}
			if ( cLine.Contains("CREATE") ) {
				throw new InvalidOperationException("CREATEが含まれています");
			}
			if ( cLine.Contains("DROP") ) {
				throw new InvalidOperationException("DROPが含まれています");
			}
			if ( cLine.Contains("EXEC") ) {
				throw new InvalidOperationException("EXECが含まれています");
			}
			if ( cLine.Contains("TRUNCATE") ) {
				throw new InvalidOperationException("TRUNCATEが含まれています");
			}
			if ( cLine.Contains("BULK") ) {
				throw new InvalidOperationException("BULKが含まれています");
			}
			if ( cLine.Contains("GRANT") ) {
				throw new InvalidOperationException("GRANTが含まれています");
			}

			//パラメータ置き換え
			int i = 1;
			cLine = sbSQL.ToString();
			foreach ( object cWork in argParam ) {
				if ( cWork != null ) {
					cLine = cLine.Replace("@p" + i.ToString(), cWork.ToString());
				}
				i++;
			}

			return cLine;
		}

		/*---------------------------------------------------------------------
		 * クエリ実行後、一行一項目取得
		 *---------------------------------------------------------------------*/
		public static object Dlookup(string argSQL, string argConName = "LocalSqlServer")
		{
			using ( SqlConnection oConn = new SqlConnection(ConfigurationManager.ConnectionStrings[argConName].ConnectionString) ) {
				using ( SqlCommand oComm = new SqlCommand(argSQL, oConn) ) {
					oConn.Open();
					object oval = oComm.ExecuteScalar();
					if ( oval == null || oval.GetType() == typeof(DBNull) ) {
						return null;
					}
					else {
						return oval;
					}
				}
			}
		}

		/*---------------------------------------------------------------------
		 * DESCRIPTION ：Publicなクエリ発行
		 * INPUT       ：argSQL : 実行するクエリ
		 * RETURN      ：-1 失敗
		 *               0< 影響を受けた行数
		 * ATTENTION   ：
		 * NOTE        ：
		 *---------------------------------------------------------------------*/
		public static int ExeQuery(string argSQL, string argConName = "LocalSqlServer")
		{
			using ( SqlConnection oConn = new SqlConnection(ConfigurationManager.ConnectionStrings[argConName].ConnectionString) ) {
				using ( SqlCommand oComm = new SqlCommand(argSQL, oConn) ) {
					oConn.Open();
					try {
						return oComm.ExecuteNonQuery();
					}
					catch {
						//						GlobalApp.appLog(argSQL);
						throw;
					}
				}
			}
		}

		/*---------------------------------------------------------------------
		 *	処理概要	：DBのログテーブルへ書き込み
		 *  insFullLog(argProcess:"同期後調整", argSubprc:oComm.CommandText, argResult:"NG", argNote:ex.Message);
		 * C#では、インスタンスを通してstaticメソッドを呼び出すことはできないので注意
		 *---------------------------------------------------------------------*/
		public static void InsDmsBatLog(
			string argProcess = "",
			string argSubprc = "",
			DateTime? argSdate = null,
			DateTime? argEdate = null,
			string argResult = "",
			int? argCnt = null,
			int? argEnum = null,
			string argNote = ""
		)
		{
			string cSQL =
				"INSERT INTO batch_status(process, subprc, sdate, edate, result, cnt, enum, note) " +
				"VALUES(@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)";

			//datetimeにnullをセットできない？System.Data.SqlTypes.SqlDateTime.Null＝「1900/01/01」ので苦肉の策
			if ( argSdate == null ) {
				if ( argResult != String.Empty && argResult != "info" ) {
					argSdate = DateTime.Now;
				}
				else {
					cSQL = cSQL.Replace("@p3", "NULL");
				}
			}
			if ( argEdate == null ) {
				cSQL = cSQL.Replace("@p4", "NULL");
			}

			using ( SqlConnection oConn = new SqlConnection(ConfigurationManager.ConnectionStrings["LocalSqlServer"].ConnectionString) ) {
				using ( SqlCommand oComm = new SqlCommand(cSQL, oConn) ) {
					oComm.Parameters.Add(new SqlParameter("@p1", SqlDbType.NVarChar, 50));
					oComm.Parameters.Add(new SqlParameter("@p2", SqlDbType.NVarChar, 50));
					if ( argSdate != null ) {
						oComm.Parameters.Add(new SqlParameter("@p3", SqlDbType.DateTime));
						oComm.Parameters["@p3"].Value = argSdate;
					}
					if ( argEdate != null ) {
						oComm.Parameters.Add(new SqlParameter("@p4", SqlDbType.DateTime));
						oComm.Parameters["@p4"].Value = argEdate;
					}
					oComm.Parameters.Add(new SqlParameter("@p5", SqlDbType.NVarChar, 50));
					oComm.Parameters.Add(new SqlParameter("@p6", SqlDbType.Int));
					oComm.Parameters.Add(new SqlParameter("@p7", SqlDbType.Int));
					oComm.Parameters.Add(new SqlParameter("@p8", SqlDbType.NVarChar, 2048));

					oComm.Parameters["@p1"].Value = (argProcess == String.Empty) ? System.Data.SqlTypes.SqlString.Null : argProcess;
					oComm.Parameters["@p2"].Value = (argSubprc == String.Empty) ? System.Data.SqlTypes.SqlString.Null : argSubprc;
					oComm.Parameters["@p5"].Value = (argResult == String.Empty) ? System.Data.SqlTypes.SqlString.Null : argResult;
					oComm.Parameters["@p6"].Value = (argCnt == null) ? System.Data.SqlTypes.SqlInt32.Null : (int)argCnt;
					oComm.Parameters["@p7"].Value = (argEnum == null) ? System.Data.SqlTypes.SqlInt32.Null : (int)argEnum;
					oComm.Parameters["@p8"].Value = (argNote == String.Empty) ? System.Data.SqlTypes.SqlString.Null : argNote;

					oConn.Open();
					oComm.ExecuteNonQuery();
				}
			}
		}

		/*---------------------------------------------------------------------
		 * DESCRIPTION ：public
		 *---------------------------------------------------------------------*/
		/*---------------------------------------------------------------------
		 * DESCRIPTION ：テーブル情報作成
		 * INPUT       ：対象なテーブル名
		 * RETURN      ：
		 * ATTENTION   ：
		 * NOTE        ：
		 *---------------------------------------------------------------------*/
		public virtual void CreateInfo(string argTable)
		{
			if ( this._connectionString == String.Empty ) {
				throw new InvalidOperationException("接続文字列がありません");
			}

			//ハッシュクリア
			this.gColOrd.Clear();
			//テーブル名保存
			this._tableName = argTable;
			//項目情報クリア
			if ( this._colInfo == null ) {
				this._colInfo = new DataTable();

				this._colInfo.Columns.Add("ColumnName", typeof(string));
				this._colInfo.Columns.Add("ColumnOrdinal", typeof(int));
				this._colInfo.Columns.Add("DataTypeName", typeof(string));
				this._colInfo.Columns.Add("ColumnSize", typeof(int));
				this._colInfo.Columns.Add("IsKey", typeof(bool));
				this._colInfo.Columns.Add("IsIdentity", typeof(bool));
				this._colInfo.Columns.Add("Value", typeof(string));
				this._colInfo.Columns.Add("UpdInfo", typeof(bool));
			}
			else {
				this._colInfo.Clear();
			}
			//クエリクリア
			this.sbSQLCmd.Clear();


			using ( SqlConnection oConn = new SqlConnection(this._connectionString) ) {
				oConn.Open();

				using ( SqlCommand oComm = new SqlCommand("SELECT TOP 1 * FROM [" + this._tableName + "] WITH(NOLOCK)", oConn) ) {
					using ( SqlDataReader oRec = oComm.ExecuteReader(CommandBehavior.KeyInfo) ) {

						//テーブル情報セット
						DataTable schemaTable = oRec.GetSchemaTable();
						int i = 0;

						foreach ( DataRow schemaField in schemaTable.Rows ) {
							if ( (bool)schemaField["IsHidden"] ) {
								continue;
							}

							this._colInfo.Rows.Add(
								schemaField["ColumnName"],
								schemaField["ColumnOrdinal"],
								schemaField["DataTypeName"],
								schemaField["ColumnSize"],
								schemaField["IsKey"],
								schemaField["IsIdentity"],
								"",
								false
							);

							this.gColOrd.Add(schemaField["ColumnName"].ToString(), i);
							i++;
						}
					}
				}
			}
		}

		public string Fget(string argColNm)
		{
			return (string)this._colInfo.Rows[this.gColOrd[argColNm]]["Value"];
		}

		/*-----------------------------------------------------------------------
		 * DESCRIPTION : 列(oColVal)に値をセット
		 * INPUT       : argColNm  : 項目名(ハッシュキー)
		 *               argFlag   : UPDATE対象
		 *               argReq    : 値１
		 *               argReq2   : 値２ argReqが空の場合、argReq2を入れる
		 *               argType   : createSQLvalのargType
		 * RETURN      :
		 * NOTE        :
		 * ATTENTION   :
		 *-----------------------------------------------------------------------*/
		public void Fput(string argColNm, bool argFlag = false, string argReq = "", string argReq2 = "", string argType = "")
		{
			if ( this._colInfo == null ) {
				throw new InvalidOperationException("createInfo()未実行です。");
			}

			if ( this.gColOrd.ContainsKey(argColNm) == false ) {
				throw new InvalidOperationException("無効な列名です:" + argColNm);
			}

			//更新用の値セット
			int i = this.gColOrd[argColNm];

			this._colInfo.Rows[i]["Value"] = CreateSQLval(i, (String.IsNullOrEmpty(argReq) ? argReq2 : argReq), argType);
			//更新可否
			this._colInfo.Rows[i]["UpdInfo"] = argFlag;
		}

		//列名でなく列位置版
		public void Fput(int argColPos, bool argFlag = false, string argReq = "", string argReq2 = "", string argType = "")
		{
			if ( this._colInfo == null ) {
				throw new InvalidOperationException("createInfo()未実行です。");
			}
			/* indexが不正なら結局例外になるのでいらない
					if ( this._colInfo.Rows.Count <= argColPos ) {
						throw new InvalidOperationException("無効な列位置です:" + argColPos.ToString());
					}
			*/
			this._colInfo.Rows[argColPos]["Value"] = CreateSQLval(argColPos, (String.IsNullOrEmpty(argReq) ? argReq2 : argReq), argType);
			//更新可否
			this._colInfo.Rows[argColPos]["UpdInfo"] = argFlag;
		}

		/*-----------------------------------------------------------------------
		 * DESCRIPTION : DB挿入用クエリ作成＆実行(exeSQL)
		 * INPUT/RETURN: argFlg  : True  : 挿入時にIDENTITYで設定された値
		 *                         False : -1 失敗
		 *                                 0< 影響を受けた行数
		 * NOTE        :
		 * ATTENTION   :
		 *-----------------------------------------------------------------------*/
		public virtual int Insert(bool returnIdentity = false)
		{
			if ( this._colInfo == null ) {
				throw new InvalidOperationException("createInfo()未実行です。");
			}

			string cVal;

			this.sbSQLCmd.Clear();

			this.sbSQLCmd.Append("INSERT INTO [" + this._tableName + "](");

			for ( int i = 0; i < this._colInfo.Rows.Count; i++ ) {
				//Identity項目は飛ばす
				if ( (bool)this._colInfo.Rows[i]["IsIdentity"] == true ) {
					continue;
				}

				this.sbSQLCmd.Append("[" + (string)this._colInfo.Rows[i]["ColumnName"] + "]");
				if ( i < (this._colInfo.Rows.Count - 1) ) {
					this.sbSQLCmd.Append(",");
				}
				else {
					this.sbSQLCmd.Append(")");
				}
			}

			/* 値 */
			this.sbSQLCmd.Append(" VALUES(");

			for ( int i = 0; i < this._colInfo.Rows.Count; i++ ) {
				//Identity項目は飛ばす
				if ( (bool)this._colInfo.Rows[i]["IsIdentity"] == true ) {
					continue;
				}

				cVal = (string)this._colInfo.Rows[i]["Value"];
				if ( String.IsNullOrEmpty(cVal) ) {
					//キーの場合
					if ( (bool)this._colInfo.Rows[i]["IsKey"] == true ) {
						this.sbSQLCmd.Append("''");
					}
					else {
						this.sbSQLCmd.Append("NULL");
					}
				}
				else {
					switch ( (string)this._colInfo.Rows[i]["DataTypeName"] ) {
					case "char":
					case "varchar":
						this.sbSQLCmd.Append("'" + cVal.Replace("'", "''") + "'");
						break;
					case "nchar":
					case "nvarchar":
						this.sbSQLCmd.Append("N'" + cVal.Replace("'", "''") + "'");
						break;
					case "smalldatetime":
					case "datetime":
					case "date":
						//GETDATE()等
						if ( cVal.Contains("(") ) {
							this.sbSQLCmd.Append(cVal);
						}
						else {
							this.sbSQLCmd.Append("'" + cVal + "'");
						}
						break;
					default:
						this.sbSQLCmd.Append(cVal);
						break;
					}
				}

				if ( i < (this._colInfo.Rows.Count - 1) ) {
					this.sbSQLCmd.Append(",");
				}
				else {
					this.sbSQLCmd.Append(")");
				}
			}

			return ExeSQL(returnIdentity);
		}

		/*-----------------------------------------------------------------------
		 * DESCRIPTION : DB更新用クエリ作成＆実行(exeSQL)
		 * INPUT       : argWHERE : UPDATEのWHERE句
		 *                          空欄の場合、テーブルのキーから where 句を生成
		 * RETURN      : -1 失敗
		 *               0< 影響を受けた行数
		 * NOTE        :
		 * ATTENTION   :
		 *-----------------------------------------------------------------------*/
		public virtual int Update(string argWHERE = "")
		{
			if ( this._colInfo == null ) {
				throw new InvalidOperationException("createInfo()未実行です。");
			}

			this.sbSQLCmd.Clear();

			this.sbSQLCmd.Append("UPDATE [" + this._tableName + "] SET ");
			for ( int i = 0; i < this._colInfo.Rows.Count; i++ ) {
				//Updateフラグが偽なら次
				if ( (bool)this._colInfo.Rows[i]["UpdInfo"] == false ) {
					continue;
				}

				this.sbSQLCmd.Append("[" + (string)this._colInfo.Rows[i]["ColumnName"] + "]=");

				string cVal = (string)this._colInfo.Rows[i]["Value"];

				if ( String.IsNullOrEmpty(cVal) ) {
					//キーの場合
					if ( (bool)this._colInfo.Rows[i]["IsKey"] == true ) {
						this.sbSQLCmd.Append("''");
					}
					else {
						this.sbSQLCmd.Append("NULL");
					}
				}
				else {
					switch ( (string)this._colInfo.Rows[i]["DataTypeName"] ) {
					case "char":
					case "varchar":
						this.sbSQLCmd.Append("'" + cVal.Replace("'", "''") + "'");
						break;
					case "nchar":
					case "nvarchar":
						this.sbSQLCmd.Append("N'" + cVal.Replace("'", "''") + "'");
						break;
					case "smalldatetime":
					case "datetime":
					case "date":
						//GETDATE()等
						if ( cVal.Contains("(") ) {
							this.sbSQLCmd.Append(cVal);
						}
						else {
							this.sbSQLCmd.Append("'" + cVal + "'");
						}
						break;
					default:
						this.sbSQLCmd.Append(cVal);
						break;
					}
				}

				this.sbSQLCmd.Append(",");
			}

			//一番最後のカンマ削除
			this.sbSQLCmd.Remove(this.sbSQLCmd.Length - 1, 1);
			this.sbSQLCmd.Append(" ");

			//WHERE句
			if ( String.IsNullOrWhiteSpace(argWHERE) ) {
				argWHERE = CreateWhereDivision();

				if ( String.IsNullOrWhiteSpace(argWHERE) ) {
					return -1;
				}
			}

			this.sbSQLCmd.Append(argWHERE);

			return ExeSQL();
		}

		/*-----------------------------------------------------------------------
		 * DESCRIPTION : DB削除用クエリ作成＆実行(exeSQL)
		 * INPUT       : argWHERE : DELETEのWHERE句
		 * RETURN      : -1 失敗
		 *               0< 影響を受けた行数
		 * NOTE        :
		 * ATTENTION   :
		 *-----------------------------------------------------------------------*/
		public int Del(string argWHERE = "")
		{
			if ( this._connectionString == String.Empty ) {
				throw new InvalidOperationException("接続文字列がありません");
			}

			this.sbSQLCmd.Clear();

			//WHERE句
			if ( String.IsNullOrWhiteSpace(argWHERE) ) {
				argWHERE = CreateWhereDivision();

				if ( String.IsNullOrWhiteSpace(argWHERE) ) {
					return -1;
				}
			}

			this.sbSQLCmd.Append("DELETE FROM [" + this._tableName + "] ");
			this.sbSQLCmd.Append(argWHERE);

			return ExeSQL();
		}

		/*-----------------------------------------------------------------------
		 * DESCRIPTION : キーに一致するデータ件数
		 *               キー値でのカウントなので２以上はありえない
		 * INPUT       :
		 * RETURN      :  1 - 更新
		 *                0 - 新規
		 *               -1 - 失敗
		 * NOTE        : insert|update判定に使用
		 * ATTENTION   :
		 *-----------------------------------------------------------------------*/
		public int CountKeyRow()
		{
			string cWHERE = CreateWhereDivision(true);
			if ( String.IsNullOrWhiteSpace(cWHERE) ) {
				return -1;
			}

			//カウント
			object oret = Dlookup("SELECT COUNT(*) FROM [" + this._tableName + "] " + cWHERE);
			if ( oret == null ) {
				return -1;
			}
			return Convert.ToInt32(oret);
		}

		/*-----------------------------------------------------------------------
		 * DESCRIPTION : DB挿入/更新用クエリ作成＆実行(exeSQL)
		 * INPUT       : argWHERE : UPDATEのWHERE句
		 * RETURN      : -1 失敗
		 *               0< 影響を受けた行数
		 * NOTE        :
		 * ATTENTION   :
		 *-----------------------------------------------------------------------*/
		public int InsOrUpd(bool returnIdentity = false, string argWHERE = "")
		{
			if ( this._colInfo == null ) {
				throw new InvalidOperationException("createInfo()未実行です。");
			}

			//キー値のWHERE句を作成
			if ( String.IsNullOrWhiteSpace(argWHERE) ) {
				argWHERE = CreateWhereDivision();
				if ( String.IsNullOrWhiteSpace(argWHERE) ) {
					throw new InvalidOperationException("キーが未指定のテーブルに対して実行できません");
				}
			}

			//カウント
			object oret = Dlookup("SELECT COUNT(*) FROM [" + this._tableName + "] " + argWHERE);
			if ( oret == null ) {
				return -1;
			}

			switch ( Convert.ToInt32(oret) ) {
			case 0:
				//insert
				return Insert(returnIdentity);

			case 1:
				//update
				return Update(argWHERE);

			default:
				throw new InvalidOperationException("キーが複数存在します");
			}
		}

		//キー(項目名)が存在するか否か True:存在
		public bool IsKey(string argColNm)
		{
			return this.gColOrd.ContainsKey(argColNm);
		}

		public string DebugInfo()
		{
			StringBuilder sbinfo = new StringBuilder();

			sbinfo.Append("<table class='HYOU'><caption>" + this._tableName + " 列数:" + this._colInfo.Rows.Count.ToString() + "</caption><thead><tr>");
			sbinfo.Append("<th></th>");
			sbinfo.Append("<th>ColumnName</th>");
			sbinfo.Append("<th>ColumnOrdinal</th>");
			sbinfo.Append("<th>DataType</th>");
			sbinfo.Append("<th>ColumnSize</th>");
			sbinfo.Append("<th>IsKey</th>");
			sbinfo.Append("<th>IsIdentity</th>");
			sbinfo.Append("<th>Value</th>");
			sbinfo.Append("<th>UpdInfo</th>");
			sbinfo.Append("</tr></thead><tbody>");

			for ( int i = 0; i < this._colInfo.Rows.Count; i++ ) {
				string cWork = (string)this._colInfo.Rows[i]["ColumnName"];

				//更新
				if ( (bool)this._colInfo.Rows[i]["UpdInfo"] ) {
					sbinfo.Append("<tr class='rowodd'>");
				}
				else {
					sbinfo.Append("<tr>");
				}

				sbinfo.Append("<td>[" + this.gColOrd[cWork].ToString() + "]</td>");
				sbinfo.Append("<td class='AL'>" + cWork + "</td>");
				sbinfo.Append("<td>" + this._colInfo.Rows[i]["ColumnOrdinal"].ToString() + "</td>");
				sbinfo.Append("<td>" + (string)this._colInfo.Rows[i]["DataTypeName"] + "</td>");
				sbinfo.Append("<td>" + this._colInfo.Rows[i]["ColumnSize"].ToString() + "</td>");

				if ( (bool)this._colInfo.Rows[i]["IsKey"] ) {
					sbinfo.Append("<td>" + this._colInfo.Rows[i]["IsKey"].ToString() + "</td>");
				}
				else {
					sbinfo.Append("<td><span class='dis'>" + this._colInfo.Rows[i]["IsKey"].ToString() + "</span></td>");
				}

				if ( (bool)this._colInfo.Rows[i]["IsIdentity"] ) {
					sbinfo.Append("<td>" + this._colInfo.Rows[i]["IsIdentity"].ToString() + "</td>");
				}
				else {
					sbinfo.Append("<td><span class='dis'>" + this._colInfo.Rows[i]["IsIdentity"].ToString() + "</span></td>");
				}

				sbinfo.Append("<td class='AL'>" + ((string)this._colInfo.Rows[i]["Value"]).Replace("\n", "<br>") + "<span class='dis W'>&#0239;</span></td>");
				sbinfo.Append("<td>" + this._colInfo.Rows[i]["UpdInfo"].ToString() + "</td>");
				sbinfo.Append("</tr>");
			}

			sbinfo.Append("</tbody></table>");
			sbinfo.Append(this.sbSQLCmd.ToString());

			return sbinfo.ToString();
		}


		/*---------------------------------------------------------------------
		 * DESCRIPTION ：local
		 *---------------------------------------------------------------------*/

		/*-----------------------------------------------------------------------
		 * DESCRIPTION : キー値でWHERE句を作成
		 * INPUT       : IsUpdateKeyValue - キー値の更新許可/不許可
		 * RETURN      : string.Empty - 失敗
		 *               その他       - 成功
		 * NOTE        :
		 * ATTENTION   :
		 *-----------------------------------------------------------------------*/
		protected string CreateWhereDivision(bool IsUpdateKeyValue = false)
		{
			string cWHERE = "";

			//キーで自動作成
			for ( int i = 0; i < this._colInfo.Rows.Count; i++ ) {
				//キー以外は飛ばす
				if ( (bool)this._colInfo.Rows[i]["IsKey"] == false ) {
					continue;
				}

				string cColName = (string)this._colInfo.Rows[i]["ColumnName"];
				string cVal = (string)this._colInfo.Rows[i]["Value"];

				//キー値の更新
				if ( IsUpdateKeyValue == false ) {
					//Updateフラグが真なら
					if ( (bool)this._colInfo.Rows[i]["UpdInfo"] ) {
						throw new InvalidOperationException("キーを更新する場合where句の自動作成はできません:" + this._tableName + "." + cColName);
					}
				}
				//値の確認
				if ( String.IsNullOrEmpty(cVal) ) {
					throw new ArgumentException("必須項目を埋めてください:" + cColName);
				}

				if ( cWHERE.Contains("WHERE") ) {
					cWHERE += " AND [" + cColName + "]='" + cVal.Replace("'", "''") + "'";
				}
				else {
					cWHERE += "WHERE [" + cColName + "]='" + cVal.Replace("'", "''") + "'";
				}
			}

			if ( !cWHERE.Contains("WHERE") ) {
				throw new InvalidOperationException("where句がありません:" + cWHERE);
			}

			return cWHERE;
		}

		/*-----------------------------------------------------------------------
		 * DESCRIPTION : SQL実行
		 * INPUT       : returnIdentity  : True  : 挿入時にIDENTITYで設定された値
		 *                                 False : -1 失敗
		 *                                          0< 影響を受けた行数
		 * RETURN      :
		 * NOTE        :
		 * ATTENTION   :
		 *-----------------------------------------------------------------------*/
		protected virtual int ExeSQL(bool returnIdentity = false)
		{
			if ( this._connectionString == String.Empty ) {
				throw new InvalidOperationException("接続文字列がありません");
			}

			using ( SqlConnection oConn = new SqlConnection(this._connectionString) ) {
				using ( SqlCommand oComm = oConn.CreateCommand() ) {
					oConn.Open();

					/* public override int ExecuteNonQuery()
					 * SCOPE_IDENTITYの戻り値の型は『numeric(38,0)』→decimal
					 */
					int iret;

					try {
						if ( returnIdentity ) {
							oComm.CommandText = this.sbSQLCmd.ToString() + ";SELECT CAST(scope_identity() AS int);";
							iret = Convert.ToInt32(oComm.ExecuteScalar());
						}
						else {
							oComm.CommandText = this.sbSQLCmd.ToString();
							iret = oComm.ExecuteNonQuery();
						}
					}
					catch ( OverflowException ) {
						iret = Int32.MaxValue;
					}

					return iret;
				}
			}
		}

		/*-----------------------------------------------------------------------
		 * NAME        : createSQLval
		 * DESCRIPTION : 文字列をSQL用にする
		 * INPUT       : argVal   : 値
		 *               argType  : 値の種類
		 *                         i  : 数値
		 *                         c  : 文字列
		 *                               t : 前後の空白削除
		 *                               v : 改行削除
		 *                               n : 半角変換
		 *                         d  : 日付（YYYY/MM/DD）
		 *                         'NULL'の場合DefaultでNULLをセット
		 * RETURN      : SQL用に変換した値
		 * NOTE        :
		 * ATTENTION   : 数値や日付でエラーのものはNULLで登録する
		 * EXAMPLE     :
		 * CREATION    : 2006/07/26
		 * HISTORY     :
		 *-----------------------------------------------------------------------*/
		protected virtual string CreateSQLval(int argColIdx, string argVal, string argType)
		{
			if ( this._colInfo == null ) {
				throw new InvalidOperationException("createInfo()未実行です。");
			}

			string cWork = argVal;

			//DataType判断
			switch ( (string)this._colInfo.Rows[argColIdx]["DataTypeName"] ) {
			case "char":
			case "nchar":
			case "varchar":
			case "nvarchar":
				//タブは無条件削除
				cWork = cWork.Replace("\t", "");

				//Trim
				if ( argType.Contains("t") ) {
					cWork = cWork.Trim();
				}

				//改行等削除
				if ( argType.Contains("v") ) {
					cWork = cWork.Replace("\r", "");
					cWork = cWork.Replace("\n", "");
				}

				//空欄は無条件でNULL
				//char型(スペースも有効な文字列)もあるのでIsNullOrWhiteSpace判定はしない
				if ( argVal == String.Empty ) {
					return "";
				}

				//HTMLの特殊文字
				if ( !argType.Contains("ND") ) {
					//&nbsp;は(U+00A0)でスペース(U+0020)とは別の文字なのでHtmlDecodeで変換しない
					//&amp;はJavaScriptで変換(&#8470;→&amp;#8470;)しているので最初に行う
					cWork = System.Web.HttpUtility.HtmlDecode(cWork.Replace("&amp;", "&").Replace("&nbsp;", " "));
				}

				//半角へ
				if ( argType.Contains("n") ) {
					cWork = MbString.Mb_convert_kana(cWork, "rnmsk");
				}
				//全角へ
				if ( argType.Contains("w") ) {
					cWork = MbString.Mb_convert_kana(cWork, "RNMSK");
				}

				//漢数字〇(SJIS:0x815A)(U+3007)はSQL Serverでは未対応なので置き換える
				cWork = cWork.Replace('\u3007', '○');
				//no-break space
				cWork = cWork.Replace('\u00A0', ' ');
				//波ダッシュ(U+301C)と、全角チルダ(U+FF5E) SQLserverはSJISの照合順序なので変換
				//Windowsでは波ダッシュは全角チルダとして扱われる？※本来は波ダッシュの方が正解
				cWork = cWork.Replace('\u301C', '～');

				//データサイズ
				//MAXの場合はチェックしない
				if ( (int)this._colInfo.Rows[argColIdx]["ColumnSize"] == Int32.MaxValue ) {
					return cWork;
				}

				int istrlen;

				if ( ((string)this._colInfo.Rows[argColIdx]["DataTypeName"]).StartsWith("n") ) {
					istrlen = cWork.Length;
				}
				else {
					istrlen = MbString.Mb_strlen(cWork);
				}

				if ( (int)this._colInfo.Rows[argColIdx]["ColumnSize"] < istrlen ) {
					/*
					GlobalApp.appLog((string)this._colInfo.Rows[argColIdx]["DataTypeName"] + "の文字が長すぎます",
								"列名 : " + this._tableName + "." + (string)this._colInfo.Rows[argColIdx]["ColumnName"],
								"列サイズ : " + this._colInfo.Rows[argColIdx]["ColumnSize"].ToString(),
								"指定値 : " + cWork,
								"length : " + istrlen.ToString()
					);
					*/
					throw new ArgumentException("文字が長すぎます。:" + (string)this._colInfo.Rows[argColIdx]["ColumnName"]);
					//cWork = DRSApp1.MbString.Mb_strcut(cWork, (int)this._colInfo.Rows[argColIdx]["ColumnSize"]);
				}
				return cWork;

			case "tinyint":
			case "smallint":
			case "int":
			case "bigint":
			case "float":
			case "real":
			case "decimal":
			case "numeric":
			case "smallmoney":
			case "money":
				//空文字数値の場合はエラーになるので強制NULLセットする
				if ( String.IsNullOrWhiteSpace(argVal) ) {
					return "";
				}

				//半角へ
				cWork = MbString.Mb_convert_kana(cWork, "nm").Trim();

				Double iVal;
				if ( Double.TryParse(cWork.Replace("\\", ""), out iVal) == false ) {
					throw new ArgumentException("数値を指定してください。:" + (string)this._colInfo.Rows[argColIdx]["ColumnName"]);
				}

				switch ( (string)this._colInfo.Rows[argColIdx]["DataTypeName"] ) {
				case "tinyint":
					if ( iVal < 0 || 255 < iVal ) {
						throw new ArgumentException("数値が大きすぎます:" + (string)this._colInfo.Rows[argColIdx]["ColumnName"]);
					}
					break;
				case "smallint":
					if ( iVal < Int16.MinValue || Int16.MaxValue < iVal ) {
						throw new ArgumentException("数値が大きすぎます:" + (string)this._colInfo.Rows[argColIdx]["ColumnName"]);
					}
					break;
				}

				return iVal.ToString();

			case "smalldatetime":
			case "datetime":
			case "date":
				if ( argVal.ToUpper() == "GETDATE()" || String.IsNullOrWhiteSpace(argVal) ) {
					return argVal.Trim();
				}

				DateTime localTime;

				//yyyyMMdd
				int iWork;
				if ( argVal.Length == 8 && Int32.TryParse(argVal, out iWork) ) {
					if ( DateTime.TryParseExact(argVal, "yyyyMMdd",
							System.Globalization.CultureInfo.InvariantCulture,
							System.Globalization.DateTimeStyles.None,
							out localTime
						) == false
					) {
						throw new ArgumentException("日付を指定してください:" + (string)this._colInfo.Rows[argColIdx]["ColumnName"]);
					}
				}

				//その他の日付
				else {
					if ( DateTime.TryParse(argVal, out localTime) == false ) {
						throw new ArgumentException("日付を指定してください:" + (string)this._colInfo.Rows[argColIdx]["ColumnName"]);
					}
				}

				if ( (string)this._colInfo.Rows[argColIdx]["DataTypeName"] == "datetime" ) {
					return localTime.ToString("G");
				}
				else {
					return localTime.ToString("d");
				}
			case "bit":
				if ( String.IsNullOrWhiteSpace(argVal) ) {
					return "";
				}

				switch ( argVal.ToLower() ) {
				case "true":
				case "1":
					return "1";
				case "false":
				case "0":
					return "0";
				default:
					throw new ArgumentException("真偽を指定してください:" + (string)this._colInfo.Rows[argColIdx]["ColumnName"]);
				}
			}

			return argVal;
		}

		//create_one_colのローカル版
		protected object Dlookup(string argSQL)
		{
			using ( SqlConnection oConn = new SqlConnection(this._connectionString) ) {
				using ( SqlCommand oComm = new SqlCommand(argSQL, oConn) ) {
					oConn.Open();
					object oval = oComm.ExecuteScalar();
					if ( oval == null || oval.GetType() == typeof(DBNull) ) {
						return null;
					}
					else {
						return oval;
					}
				}
			}
		}
	}
}