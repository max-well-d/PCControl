function ScreenShot() {
	sc = document.getElementById("sc")
	var timestamp = new Date().getTime();
	sc.src = "./screenshot.jpg?t=" + timestamp;
};
if (window.history.replaceState) {
	window.history.replaceState(null, null, window.location.href);
};