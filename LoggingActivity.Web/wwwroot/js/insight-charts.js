// Helper Chart.js dùng chung cho Dashboard và các trang báo cáo: bảng màu, tooltip, gradient, sparkline, chữ giữa biểu đồ vòng.
(function (global) {
	"use strict";

	var palette = ["#3699ff", "#0f766e", "#7239ea", "#d97706", "#f1416c", "#15803d", "#0891b2", "#94a3b8"];
	var otherLabel = "Khác";
	var otherColor = "#cbd5e1";
	var gridColor = "rgba(148, 163, 184, 0.18)";
	var textColor = "#5e6278";
	var headingColor = "#181c32";
	var numberFormat = new Intl.NumberFormat("vi-VN");

	function formatNumber(value) {
		return numberFormat.format(value || 0);
	}

	function colorFor(label, index) {
		return label === otherLabel ? otherColor : palette[index % palette.length];
	}

	function withAlpha(hex, alphaHex) {
		return hex.length === 7 ? hex + alphaHex : hex;
	}

	function tooltip(extra) {
		return Object.assign({
			backgroundColor: "rgba(17, 24, 39, 0.94)",
			titleColor: "#f9fafb",
			bodyColor: "#e5e7eb",
			footerColor: "#f9fafb",
			padding: 12,
			cornerRadius: 10,
			usePointStyle: true,
			boxPadding: 4,
			titleFont: { size: 13, weight: "700" },
			bodyFont: { size: 12, weight: "600" },
			footerFont: { size: 12, weight: "700" }
		}, extra || {});
	}

	function applyDefaults() {
		if (typeof Chart === "undefined" || Chart.__insightDefaultsApplied) {
			return;
		}

		Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
		Chart.defaults.color = textColor;
		Chart.defaults.maintainAspectRatio = false;
		Chart.defaults.responsive = true;
		Chart.defaults.animation.duration = 700;
		Chart.__insightDefaultsApplied = true;
	}

	// Gradient dọc từ trong suốt lên màu nhạt, dùng cho vùng dưới đường.
	function verticalGradient(context, color, topAlpha) {
		var chart = context.chart;
		if (!chart.chartArea) {
			return withAlpha(color, "22");
		}

		var gradient = chart.ctx.createLinearGradient(0, chart.chartArea.bottom, 0, chart.chartArea.top);
		gradient.addColorStop(0, withAlpha(color, "00"));
		gradient.addColorStop(1, withAlpha(color, topAlpha || "55"));
		return gradient;
	}

	// Gradient ngang cho thanh bar nằm ngang.
	function horizontalGradient(context, fromColor, toColor) {
		var chart = context.chart;
		if (!chart.chartArea) {
			return fromColor;
		}

		var gradient = chart.ctx.createLinearGradient(chart.chartArea.left, 0, chart.chartArea.right, 0);
		gradient.addColorStop(0, fromColor);
		gradient.addColorStop(1, toColor);
		return gradient;
	}

	var centerTextPlugin = {
		id: "insightCenterText",
		afterDraw: function (chart, args, options) {
			if (!options || !options.text) {
				return;
			}

			var meta = chart.getDatasetMeta(0);
			if (!meta || !meta.data.length) {
				return;
			}

			var ctx = chart.ctx;
			var x = meta.data[0].x;
			var y = meta.data[0].y;
			ctx.save();
			ctx.textAlign = "center";
			ctx.textBaseline = "middle";
			ctx.fillStyle = headingColor;
			ctx.font = "800 22px " + Chart.defaults.font.family;
			ctx.fillText(options.text, x, y - 8);
			ctx.fillStyle = textColor;
			ctx.font = "600 12px " + Chart.defaults.font.family;
			ctx.fillText(options.subtext || "", x, y + 14);
			ctx.restore();
		}
	};

	// Vẽ đường ngang nét đứt (ví dụ mức trung bình) kèm nhãn ở mép phải.
	var referenceLinePlugin = {
		id: "insightReferenceLine",
		afterDatasetsDraw: function (chart, args, options) {
			if (!options || typeof options.value !== "number" || options.value <= 0) {
				return;
			}

			var yScale = chart.scales[options.scaleId || "y"];
			if (!yScale) {
				return;
			}

			var y = yScale.getPixelForValue(options.value);
			var area = chart.chartArea;
			var ctx = chart.ctx;
			ctx.save();
			ctx.strokeStyle = options.color || "#f1416c";
			ctx.lineWidth = 1.5;
			ctx.setLineDash([6, 4]);
			ctx.beginPath();
			ctx.moveTo(area.left, y);
			ctx.lineTo(area.right, y);
			ctx.stroke();
			if (options.label) {
				ctx.setLineDash([]);
				ctx.font = "700 11px " + Chart.defaults.font.family;
				var textWidth = ctx.measureText(options.label).width;
				ctx.fillStyle = "rgba(255, 255, 255, 0.92)";
				ctx.fillRect(area.right - textWidth - 10, y - 18, textWidth + 8, 16);
				ctx.fillStyle = options.color || "#f1416c";
				ctx.textAlign = "right";
				ctx.textBaseline = "middle";
				ctx.fillText(options.label, area.right - 6, y - 10);
			}
			ctx.restore();
		}
	};

	function renderSparkline(canvas, labels, values, color) {
		return new Chart(canvas, {
			type: "line",
			data: {
				labels: labels,
				datasets: [{
					data: values,
					borderColor: color,
					borderWidth: 2,
					fill: true,
					backgroundColor: function (context) { return verticalGradient(context, color); },
					tension: 0.4,
					pointRadius: 0
				}]
			},
			options: {
				animation: false,
				plugins: { legend: { display: false }, tooltip: { enabled: false } },
				scales: { x: { display: false }, y: { display: false, beginAtZero: true } },
				events: []
			}
		});
	}

	// Chú thích HTML cho biểu đồ vòng: màu, nhãn, tỷ trọng.
	function renderShareLegend(container, items, colors) {
		if (!container) {
			return;
		}

		var total = items.reduce(function (sum, item) { return sum + item.value; }, 0);
		container.innerHTML = "";
		items.forEach(function (item, index) {
			var share = total === 0 ? 0 : (item.value * 100 / total);
			var li = document.createElement("li");
			var swatch = document.createElement("span");
			swatch.className = "cb-legend__swatch";
			swatch.style.background = colors[index];
			var label = document.createElement("span");
			label.className = "cb-legend__label";
			label.textContent = item.label;
			label.title = item.label;
			var value = document.createElement("span");
			value.className = "cb-legend__value";
			value.textContent = share.toFixed(1) + "%";
			li.appendChild(swatch);
			li.appendChild(label);
			li.appendChild(value);
			container.appendChild(li);
		});
	}

	function renderDoughnut(canvas, items, options) {
		options = options || {};
		var total = items.reduce(function (sum, item) { return sum + item.value; }, 0);
		var colors = items.map(function (item, index) { return colorFor(item.label, index); });

		var chart = new Chart(canvas, {
			type: "doughnut",
			data: {
				labels: items.map(function (item) { return item.label; }),
				datasets: [{
					data: items.map(function (item) { return item.value; }),
					backgroundColor: colors,
					borderColor: "#ffffff",
					borderWidth: 3,
					hoverOffset: 8
				}]
			},
			options: {
				cutout: "70%",
				plugins: {
					legend: { display: false },
					tooltip: tooltip({
						callbacks: {
							label: function (context) {
								var share = total === 0 ? 0 : (context.parsed * 100 / total);
								return " " + formatNumber(context.parsed) + " " + (options.unit || "") + " (" + share.toFixed(1) + "%)";
							}
						}
					}),
					insightCenterText: { text: formatNumber(total), subtext: options.centerSubtext || "" }
				}
			},
			plugins: [centerTextPlugin]
		});

		renderShareLegend(options.legendElement, items, colors);
		return chart;
	}

	// Chart.js đo độ rộng nhãn trục khi vẽ; nếu web font chưa tải xong thì nhãn bị cắt, nên đợi font sẵn sàng.
	function whenReady(callback) {
		document.addEventListener("DOMContentLoaded", function () {
			var fontsReady = document.fonts && document.fonts.ready ? document.fonts.ready : Promise.resolve();
			fontsReady.then(callback, callback);
		});
	}

	global.InsightCharts = {
		whenReady: whenReady,
		palette: palette,
		otherLabel: otherLabel,
		otherColor: otherColor,
		gridColor: gridColor,
		textColor: textColor,
		formatNumber: formatNumber,
		colorFor: colorFor,
		tooltip: tooltip,
		applyDefaults: applyDefaults,
		verticalGradient: verticalGradient,
		horizontalGradient: horizontalGradient,
		centerTextPlugin: centerTextPlugin,
		referenceLinePlugin: referenceLinePlugin,
		renderSparkline: renderSparkline,
		renderDoughnut: renderDoughnut,
		renderShareLegend: renderShareLegend
	};
})(window);
