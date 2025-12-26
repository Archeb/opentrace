window.opentrace = {
	Hops: [],
	map: null,
	markers: [],
	polyline: null,

	reset: function (hideMapPopup = false, darkMode = false) {
		// 根据 darkMode 切换主题
		if (darkMode) {
			document.documentElement.setAttribute('data-theme', 'dark');
		} else {
			document.documentElement.removeAttribute('data-theme');
		}

		// 使用 HTML 中预先初始化的地图实例
		if (!this.map) {
			this.map = window.osmMapInstance;
		}

		// 清除现有标记和折线
		if (this.markers && this.markers.length > 0) {
			this.markers.forEach(marker => {
				if (this.map && marker) {
					this.map.removeLayer(marker);
				}
			});
		}
		this.markers = [];
		
		if (this.polyline && this.map) {
			this.map.removeLayer(this.polyline);
			this.polyline = null;
		}

		this.Hops = [];
		this.hideMapPopup = hideMapPopup;
		if (document.getElementById("opentracePopup")) document.getElementById("opentracePopup").remove();
		
		console.log('OpenTrace reset completed, map:', this.map);
	},

	updateHop: function (hop, hopNo = undefined) {
		// 确保地图已初始化
		if (!this.map) {
			this.map = window.osmMapInstance;
		}

		// Parse the JSON string into an object
		const hopData = JSON.parse(hop);
		console.log('updateHop called:', hopData);

		// 清除现有标记和折线
		if (this.markers && this.markers.length > 0) {
			this.markers.forEach(marker => {
				if (this.map && marker) {
					this.map.removeLayer(marker);
				}
			});
		}
		this.markers = [];
		
		if (this.polyline && this.map) {
			this.map.removeLayer(this.polyline);
			this.polyline = null;
		}

		if (hopNo !== undefined) {
			// Update the existing hop if hopNo is provided
			this.Hops[hopNo] = hopData;
		} else {
			// Add the new hop to the list of hops
			this.Hops.push(hopData);
		}

		// Loop through all hops and add circle markers
		let path = [];
		for (let i = 0; i < this.Hops.length; i++) {
			if (this.Hops[i].Longitude == 0 || this.Hops[i].Latitude == 0 || this.Hops[i].Longitude == "" || this.Hops[i].Latitude == "") continue;
			let h = this.Hops[i];
			let lat = parseFloat(h.Latitude);
			let lng = parseFloat(h.Longitude);
			
			if (isNaN(lat) || isNaN(lng)) continue;
			
			let latLng = [lat, lng];
			
			// 使用 circleMarker 创建圆形标记
			let marker = L.circleMarker(latLng, {
				radius: 8,
				fillColor: '#5BA0E8',
				color: '#2E7BC7',
				weight: 2,
				opacity: 1,
				fillOpacity: 0.9
			}).addTo(this.map);
			
			marker.bindTooltip(`#${h.No}: ${h.Geolocation}`);
			let markerIndex = i;
			marker.on('mouseover', () => {
				this.showPopup(markerIndex);
			});
			this.markers.push(marker);
			path.push(latLng);
			
			console.log('Added marker at:', latLng);
		}

		// Connect the markers with a polyline
		if (path.length > 0) {
			this.polyline = L.polyline(path, {
				color: '#4A90D9',
				opacity: 0.85,
				weight: 3
			}).addTo(this.map);
			console.log('Added polyline with points:', path.length);
		}

		if (path.length > 1) {
			// Fit the map to the bounds of the path
			let bounds = L.latLngBounds(path);
			this.map.fitBounds(bounds, { padding: [20, 20] });
		} else if (path.length === 1) {
			this.map.setView(path[0], 8);
		}
	},

	focusHop: function (hopNo) {
		var hop = this.Hops[hopNo];
		if (hop == null) return;
		this.showPopup(hopNo);
		if (hop.Longitude == 0 || hop.Latitude == 0 || hop.Longitude == "" || hop.Latitude == "") return;
		
		let lat = parseFloat(hop.Latitude);
		let lng = parseFloat(hop.Longitude);
		if (isNaN(lat) || isNaN(lng)) return;
		
		this.map.setView([lat, lng], 8);
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
