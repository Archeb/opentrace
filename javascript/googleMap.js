window.opentrace = {
	Hops: [],
	reset: function (hideMapPopup = false) {
		window.gmap = new google.maps.Map(document.getElementById("map"), { center: { lat: 0, lng: 0 }, zoom: 2, disableDefaultUI: true });
		gmap.overlayMapTypes.clear();
		this.Hops = [];
		this.hideMapPopup = hideMapPopup;
		if (document.getElementById("opentracePopup")) document.getElementById("opentracePopup").remove();
	},

	updateHop: function (hop, hopNo = undefined) {

		// Parse the JSON string into an object
		const hopData = JSON.parse(hop);

		console.log(hopData);

		// Clear any existing overlays
		gmap.overlayMapTypes.clear();

        if (hopNo !== undefined) {
            // Update the existing hop if hopNo is provided
			this.Hops[hopNo] = hopData;
		} else {
			// Add the new hop to the list of hops
			this.Hops.push(hopData);
		}

		// Loop through all hops and add markers
		for (let i = 0; i < this.Hops.length; i++) {
			if (this.Hops[i].Longitude == 0 || this.Hops[i].Latitude == 0 || this.Hops[i].Longitude == "" || this.Hops[i].Latitude == "") continue;
			let h = this.Hops[i];
			let latLng = new google.maps.LatLng(h.Latitude, h.Longitude);
			let marker = new google.maps.Marker({
				position: latLng,
				map: gmap,
				title: `#${h.No}: ${h.Geolocation}`
			});
			let markerIndex = i;
			google.maps.event.addListener(marker, 'mouseover', () => {
				this.showPopup(markerIndex);
			});
		}

		// Connect the markers with a polyline
		let path = [];
		for (let i = 0; i < this.Hops.length; i++) {
			if (this.Hops[i].Longitude == 0 || this.Hops[i].Latitude == 0 || this.Hops[i].Longitude == "" || this.Hops[i].Latitude == "") continue;
			let h = this.Hops[i];
			let latLng = new google.maps.LatLng(h.Latitude, h.Longitude);
			path.push(latLng);
		}
		let polyline = new google.maps.Polyline({
			path: path,
			strokeColor: '#FF0000',
			strokeOpacity: 1.0,
			strokeWeight: 2
		});
		polyline.setMap(gmap);

		if (path.length > 1) {
			// Get the bounds of the path 
			let bounds = new google.maps.LatLngBounds();
			for (let i = 0; i < path.length; i++) {
				bounds.extend(path[i]);
			}
			// Fit the map to those bounds
			gmap.fitBounds(bounds);
		}
	},

	focusHop: function (hopNo) {
		var hop = this.Hops[hopNo];
		if (hop == null) return;
		this.showPopup(hopNo);
		if (hop.Longitude == 0 || hop.Latitude == 0 || hop.Longitude == "" || hop.Latitude == "") return;
		let hopPos = new google.maps.LatLng(hop.Latitude, hop.Longitude);
		gmap.setCenter(hopPos);
	},

	showPopup: function (hopNo) {
		var hop = this.Hops[hopNo];
		if (hop == null) return;
		if (document.getElementById("opentracePopup")) document.getElementById("opentracePopup").remove();
		if (this.hideMapPopup == true) return;
		// 创建左上角的悬浮提示
		var popupElement = document.createElement("div");
		popupElement.id = "opentracePopup";
		popupElement.style =
			"position: absolute; bottom: 10px; left: 10px; padding: 5px 15px 5px 5px; background-color: rgba(255,255,255,0.85); border: 1px solid #ccc; border-radius: 5px; z-index: 9999;";
		popupElement.innerHTML = `
		<style>
		td:first-child {
			font-weight:bold;
			padding-right:5px;
		}
		.close-button{
			position: absolute;
			right: 5px;
			top: 0;
			cursor: pointer;
			user-select: none;
		}
		</style>
		<div class="close-button">×</div>
		<table>
			<tr>
				<td style="white-space: nowrap;">IP #${hop.No}</td>
				<td>${hop.IP}</td>
			</tr>
			<tr>
				<td>Time</td>
				<td>${hop.Time}</td>
			</tr>
			<tr>
				<td>Geo</td>
				<td>${hop.Geolocation}</td>
			</tr>
			<tr>
				<td>Org</td>
				<td>${hop.Organization}</td>
			</tr>
			<tr>
				<td>AS</td>
				<td>${hop.AS}</td>
			</tr>
		</table>`;
		popupElement.querySelector(".close-button").addEventListener("click", () => document.getElementById("opentracePopup").remove());
		document.body.appendChild(popupElement);
	},
};

