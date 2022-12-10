var link = document.createElement( "link" );
link.type = "text/css";
link.rel = "stylesheet";
link.media = "screen,print";
if (/Android|webOS|iPhone|iPod|BlackBerry/i.test(navigator.userAgent)) {
	link.href = "./pe.css";
} else {
	link.href = "./pc.css";
}
	document.getElementsByTagName( "head" )[0].appendChild( link );

if(window.history.replaceState){
	window.history.replaceState(null, null, window.location.href)};
