<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Tank.Flash._Default" %>
<html>
	<head id="Head1">
		<meta HTTP-EQUIV="Pragma" content="no-cache" />
		<script type="text/javascript" src="scripts/Jquery.js"></script>
		<script type="text/javascript" src="scripts/swfobjectVer.js"></script>
		<script type="text/javascript" src="scripts/doVersion.js?637868363182347827"></script>
		<meta HTTP-EQUIV="Cache-Control" content="no-cache" />
		<meta HTTP-EQUIV="Expires" content="0" />
		<title>弹弹堂</title>
		<script type="text/javascript" src="scripts/info.js?2"></script>
		<script type="text/javascript" src="scripts/jquery.js?0"></script>
		<script type="text/javascript" src="scripts/dandantang.js?0"></script>
		<script type="text/javascript" src="scripts/rightClick.js?0"></script>
		<script type="text/javascript" src="scripts/swfobject.js?0"></script>
		<script type="text/javascript" src="scripts/common.js?0"></script>
		<script type="text/javascript" src="scripts/agent.js?0"></script>
		<script type="text/javascript" src="scripts/favorite.js?0"></script>
		<script type="text/javascript" src="scripts/title.js?0"></script>
		<script type="text/javascript" src="scripts/isSafeFlash.js?1"></script>
		<link rel=stylesheet type=text/css href="style/style.css">
	</head>
	<body>
		<table class=mhqy_game border=0 cellSpacing=0 cellPadding=0>
			<tbody>
				<tr>
					<td>
						<table class=mhqy_main border=0 cellSpacing=0 cellPadding=0>
							<tbody>
								<tr>
									<td vAlign=center align=middle>
										<div class=box>
											<object classid="clsid:d27cdb6e-ae6d-11cf-96b8-444553540000" id="7road-ddt-game" codebase="http://fpdownload.macromedia.com/pub/shockwave/cabs/flash/swflash.cab#version=8,0,0,0"
													name="Main" width="1000" height="600" align="middle" id="Main">
												<param name="allowScriptAccess" value="always" />
												<param name="movie" value='Loading.swf?<%=Content %>&v=<%=Edition %>&rand=<%=Rand %>' />
												<param name="quality" value="high" />
												<param name="menu" value="false">
												<param name="bgcolor" value="#000000" />
												<param name="FlashVars" value="<%=AutoParam + " &sex=" + Sex %>" />
												<embed flashVars="<%=AutoParam + " &sex=" + Sex %>" src='Loading.swf?<%=Content %>&v=<%=Edition %>&rand=<%=Rand %>' width="1000" height="600"
													   align="middle" quality="high" name="Main" allowscriptaccess="sameDomain" type="application/x-shockwave-flash"
													   pluginspage="http://www.macromedia.com/go/getflashplayer" />
											</object>
										</div>
									</td>
								</tr>
							</tbody>
						</table>
					</td>
				</tr>
			</tbody>
		</table>

	</body>
</html>