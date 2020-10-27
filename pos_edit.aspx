<%@ Page CodeBehind="page_base.cs" Inherits="drs_basePage" ContentType="text/html" MaintainScrollPositionOnPostback="True" %>
<%@ OutputCache Location="None" VaryByParam="None" %>
<%@ Import Namespace="System.Data" %>
<%@ Import Namespace="System.Data.SqlClient" %>
<%@ Import Namespace="System.Configuration" %>
<%@ Import Namespace="DRS.SQLUtl" %>
<script runat="server">
	private void Page_Load(object sender, EventArgs e)
	{
		//Defaultソート
		if ( gvMlist.SortExpression == String.Empty ) {
			gvMlist.Sort("sort_seq", SortDirection.Ascending);
		}
	}

	private void gvRowUpdating(object sender, GridViewUpdateEventArgs e)
	{
		e.Cancel = true;

		var			oClsSQL = new drs_sql();
		GridViewRow	oSelectedRow = ((GridView)sender).Rows[e.RowIndex];
		string		cOrgKiki  = e.Keys[0].ToString();
		bool		IsNewPos = String.IsNullOrWhiteSpace(cOrgKiki);

		oClsSQL.createInfo("POS_ms");

		oClsSQL.fput("kiki_cd",			true, ((TextBox)oSelectedRow.Cells[1].Controls[0]).Text, "", "tvn");
		oClsSQL.fput("kiki_name",		true, ((TextBox)oSelectedRow.Cells[2].Controls[0]).Text, "", "tv");
		oClsSQL.fput("kiki_ryaku_name1",true, ((TextBox)oSelectedRow.Cells[3].Controls[0]).Text, "", "tvn");
		oClsSQL.fput("kiki_ryaku_name2",true, ((TextBox)oSelectedRow.Cells[4].Controls[0]).Text, "", "tvn");
//		oClsSQL.fput("kiki_ryaku_name3");
		oClsSQL.fput("kiki_hyojiyo",	true, ((TextBox)oSelectedRow.Cells[5].Controls[0]).Text, "", "tv");
		oClsSQL.fput("fc_price",		true, ((TextBox)oSelectedRow.Cells[6].Controls[0]).Text);
		oClsSQL.fput("co_price",		true, ((TextBox)oSelectedRow.Cells[7].Controls[0]).Text);
//		oClsSQL.fput("total_cnt",		true, ((TextBox)oSelectedRow.Cells[8].Controls[0]).Text);
		oClsSQL.fput("sort_seq",		true, ((TextBox)oSelectedRow.Cells[9].Controls[0]).Text);
		oClsSQL.fput("ows_flg",			true, ((DropDownList)oSelectedRow.FindControl("ddlOWS")).SelectedValue);
		oClsSQL.fput("cho_flg",			true, ((DropDownList)oSelectedRow.FindControl("ddlCho")).SelectedValue);
		oClsSQL.fput("del_flg",			true, ((DropDownList)oSelectedRow.FindControl("ddlDsp")).SelectedValue);

		//キー変更な場合
		if ( cOrgKiki != oClsSQL.fget("kiki_cd") ) {
			if ( oClsSQL.create_one_col_int("SELECT COUNT(*) FROM POS_ms WHERE kiki_cd = '" + oClsSQL.fget("kiki_cd") + "'") != 0 ) {
				if ( IsNewPos ) {
					message.Text = "<p class='R'>商品コードが存在します。<br>処理を中断します</p>";
					return;
				}
				else {
					message.Text = "<p class='R'>変更後の商品コードが存在します。<br>処理を中断します</p>";
					return;
				}
			}

			if ( IsNewPos == false ) {
				GlobalApp.appTrace("コード変更 " + cOrgKiki + "→" + oClsSQL.fget("kiki_cd"));
			}
		}

		//更新
		int iret;

		if ( IsNewPos ) {
			oClsSQL.fput("ins_date", true, "GETDATE()");
			oClsSQL.fput("ins_user", true, HttpContext.Current.User.Identity.Name);
			iret = oClsSQL.insert();
		}
		else {
			oClsSQL.fput("upd_date", true, "GETDATE()");
			oClsSQL.fput("upd_user", true, HttpContext.Current.User.Identity.Name);
			iret = oClsSQL.update("WHERE kiki_cd = '" + cOrgKiki + "'");
		}
		if ( iret != 1 ) {
			oClsSQL.debug_info();
			GlobalApp.appTrace(oClsSQL.cMsg);

			message.Text = "<p class='R'>システムエラー：お手数ですが管理者へお知らせ下さい</p>";
			return;
		}

		message.Text = "<p class='B'>更新完了</p>";
/*
message.Text += "key:" + cOrgKiki + "<BR>";
oClsSQL.debug_info();
message.Text += oClsSQL.cMsg;
*/
		gvMlist.EditIndex = -1;
	}

	private void gvOnRowDataBound(object sender, GridViewRowEventArgs e)
	{
		if ( e.Row.RowType != DataControlRowType.DataRow ) {
			return;
		}

		if ( (e.Row.RowState & DataControlRowState.Edit) == 0 ) {
			//新規リンク
			if ( e.Row.Cells[1].Text == "&nbsp;" ) {
				((LinkButton)e.Row.Cells[0].Controls[0]).Text = "新規";
			}
			//新規以外は色付け
			else if ( ((Label)e.Row.FindControl("del_flg")).Text != String.Empty ) {
				e.Row.CssClass = "rowdel";
			}
		}
		else {
			//更新リンク
			((LinkButton)e.Row.Cells[0].Controls[0]).Attributes.Add("onclick","return IsFormVal();");
		}
	}
</script>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Frameset//EN" "http://www.w3.org/TR/html4/frameset.dtd">
<html lang="ja">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<meta http-equiv="Content-Style-Type" content="text/css">
<meta http-equiv="Content-Script-Type" content="text/javascript">
<link rel="stylesheet" type="text/css" href="ui/css/designIE9.css" />
<script type="text/javascript" src="ui/js/prototype.js"></script>
<script type="text/javascript" src="ui/js/common.js"></script>
<script type="text/javascript" src="ui/js/pos_edit.js"></script>
</head>
<title>商品マスター</title>
<body>
<form runat="server">
<div id="header"><table>
<tr><td class="head"><asp:Literal id="pageTitle" runat="Server" />　　<a class="aaa" href="help/index.html" target="main">ヘルプ</a></td></tr>
<tr><td class="menu"><ul>
	<li><a href="shiji_list.aspx">作業手配</a></li>
	<li><a href="jisseki_list.aspx">請求実績</a></li>
	<li><a href="chousei_list.aspx">調整一覧</a></li>
	<li><a href="horyu_list.aspx">請求対象外</a></li>
	<li><a href="syusei_list.aspx">当月修正分</a></li>
	<li><a href="tenpo_co_list.aspx">店舗管理</a></li>
	<li class="act"><a href="pos_edit.aspx">商品ﾏｽﾀｰ</a></li>
	<li><a href="status_edit.aspx">受付名称ﾏｽﾀｰ</a></li>
	<li class="a"><asp:LinkButton OnCommand="logout_click" Text="ログオフ" runat="Server" /></li>
</ul></td></tr>
</table></div>

<asp:Literal id="message" EnableViewState="false" runat="Server" />
<asp:GridView ID="gvMlist" DataSourceID="gvSql" CssClass="HYOU" CellSpacing="-1" GridLines="None" Runat="server"
	DataKeyNames="kiki_cd"
	AutoGenerateColumns="False"
	AllowSorting="True"
	OnRowDataBound="gvOnRowDataBound"
	OnRowUpdating="gvRowUpdating"
>
	<RowStyle CssClass="rowodd" />
	<AlternatingRowStyle CssClass="roweven" />
	<SortedAscendingHeaderStyle CssClass="sortasc" />
	<SortedDescendingHeaderStyle CssClass="sortdesc" />
<Columns>
	<asp:CommandField HeaderText=""
		ShowEditButton="True" ShowCancelButton="True" ButtonType="Link"
		EditText="編集" CancelText="中止" UpdateText="更新"
	>
		<HeaderStyle CssClass="N" />
	</asp:CommandField>

	<asp:BoundField HeaderText="商品CD" DataField="kiki_cd" SortExpression="kiki_cd">
		<ItemStyle CssClass="V" />
		<ControlStyle width="8ex" />
	</asp:BoundField>
	<asp:BoundField HeaderText="正式名称" DataField="kiki_name" SortExpression="kiki_name">
		<ItemStyle CssClass="AL" />
		<ControlStyle CssClass="W4" />
	</asp:BoundField>
	<asp:BoundField HeaderText="科目" DataField="kiki_ryaku_name1" SortExpression="kiki_ryaku_name1">
		<ItemStyle CssClass="AL" />
		<ControlStyle CssClass="W2" />
	</asp:BoundField>
	<asp:BoundField HeaderText="略称カナ" DataField="kiki_ryaku_name2" SortExpression="kiki_ryaku_name2">
		<ItemStyle CssClass="AL" />
		<ControlStyle CssClass="W2" />
	</asp:BoundField>
	<asp:BoundField HeaderText="表示用名称" DataField="kiki_hyojiyo" SortExpression="kiki_hyojiyo">
		<ItemStyle CssClass="AL" />
		<ControlStyle CssClass="W2" />
	</asp:BoundField>
	<asp:BoundField HeaderText="FC 金額" DataField="fc_price" SortExpression="fc_price" DataFormatString="{0:#,###;<font color=red>-#,###</font>;0}" HtmlEncode="False">
		<ItemStyle CssClass="AR" />
		<ControlStyle CssClass="NUM6" />
	</asp:BoundField>
	<asp:BoundField HeaderText="CO 金額" DataField="co_price" SortExpression="co_price" DataFormatString="{0:#,###;<font color=red>-#,###</font>;0}" HtmlEncode="False">
		<ItemStyle CssClass="AR" />
		<ControlStyle CssClass="NUM13" />
	</asp:BoundField>
	<asp:BoundField HeaderText="総台数" DataField="total_cnt" SortExpression="total_cnt" Visible="False">
		<ItemStyle CssClass="AR" />
		<ControlStyle CssClass="NUM13" />
	</asp:BoundField>
	<asp:BoundField HeaderText="表示順" DataField="sort_seq" SortExpression="sort_seq">
		<ItemStyle CssClass="AR" />
		<ControlStyle CssClass="NUM6" />
	</asp:BoundField>

	<asp:TemplateField HeaderText="OWS扱い" SortExpression="ows_flg">
		<ItemTemplate><asp:Literal Text='<%# myPage_setCheckCode(Eval("ows_flg"), "OWS") %>' runat="server"/></ItemTemplate>
		<EditItemTemplate>
			<asp:DropDownList id="ddlOWS" SelectedValue='<%# Bind("ows_flg") %>' runat="server">
				<asp:ListItem Value="" Text="" />
				<asp:ListItem Value="1" Text="OWS" />
			</asp:DropDownList>
		</EditItemTemplate>
	</asp:TemplateField>
	<asp:TemplateField HeaderText="調整額扱い" SortExpression="cho_flg">
		<ItemTemplate><asp:Literal Text='<%# myPage_setCheckCode(Eval("cho_flg"), "調整") %>' runat="server"/></ItemTemplate>
		<EditItemTemplate>
			<asp:DropDownList id="ddlCho" SelectedValue='<%# Bind("cho_flg") %>' runat="server">
				<asp:ListItem Value="" Text="" />
				<asp:ListItem Value="1" Text="調整" />
			</asp:DropDownList>
		</EditItemTemplate>
	</asp:TemplateField>
	<asp:TemplateField HeaderText="非表示" SortExpression="del_flg">
		<ItemTemplate>
			<asp:Label id="del_flg" Text='<%# myPage_setCheckCode(Eval("del_flg"), "非表示") %>' runat="server"/>
		</ItemTemplate>
		<EditItemTemplate>
			<asp:DropDownList id="ddlDsp" SelectedValue='<%# Bind("del_flg") %>' runat="server">
				<asp:ListItem Value="0" Text="表示" />
				<asp:ListItem Value="1" Text="非表示" />
			</asp:DropDownList>
		</EditItemTemplate>
	</asp:TemplateField>
</Columns>
</asp:GridView>
</form>
</body>
</html>
<asp:SqlDataSource ID="gvSQL" runat="server" ConnectionString="<%$ ConnectionStrings:LocalSqlServer %>"
	SelectCommand="
SELECT kiki_cd, kiki_name, kiki_ryaku_name1, kiki_ryaku_name2, kiki_hyojiyo,
	CAST(co_price AS int) AS co_price, CAST(fc_price AS int) AS fc_price,
	total_cnt, sort_seq, ows_flg, cho_flg, del_flg
FROM POS_ms
UNION
SELECT NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,99999,NULL,NULL,0
" />