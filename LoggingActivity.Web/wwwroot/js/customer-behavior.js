(function () {
	"use strict";

	var palette = ["#3699ff", "#0f766e", "#7239ea", "#d97706", "#f1416c", "#15803d", "#0891b2", "#94a3b8"];
	var otherColor = "#cbd5e1";
	var gridColor = "rgba(148, 163, 184, 0.18)";
	var textColor = "#5e6278";
	var numberFormat = new Intl.NumberFormat("vi-VN");

	function formatNumber(value) {
		return numberFormat.format(value || 0);
	}

	function colorFor(label, index) {
		return label === "Khác" ? otherColor : palette[index % palette.length];
	}

	function tooltipStyle() {
		return {
			backgroundColor: "rgba(17, 24, 39, 0.94)",
			titleColor: "#f9fafb",
			bodyColor: "#e5e7eb",
			padding: 12,
			cornerRadius: 10,
			usePointStyle: true,
			boxPadding: 4,
			titleFont: { size: 13, weight: "700" },
			bodyFont: { size: 12, weight: "600" }
		};
	}

	function applyDefaults() {
		Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
		Chart.defaults.color = textColor;
		Chart.defaults.maintainAspectRatio = false;
		Chart.defaults.responsive = true;
		Chart.defaults.animation.duration = 700;
	}

	function verticalGradient(context, color) {
		var chart = context.chart;
		if (!chart.chartArea) {
			return color;
		}

		var gradient = chart.ctx.createLinearGradient(0, chart.chartArea.bottom, 0, chart.chartArea.top);
		gradient.addColorStop(0, color + "00");
		gradient.addColorStop(1, color + "55");
		return gradient;
	}

	function renderSparklines(data) {
		document.querySelectorAll("[data-cb-spark]").forEach(function (canvas) {
			var values = data[canvas.getAttribute("data-cb-spark")] || [];
			var color = canvas.getAttribute("data-cb-color") || palette[0];
			new Chart(canvas, {
				type: "line",
				data: {
					labels: data.dailyLabels,
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
		});
	}

	function renderDailyChart(data) {
		var canvas = document.getElementById("cbDailyChart");
		if (!canvas) {
			return;
		}

		var datasets = data.dailySeries.map(function (series, index) {
			var color = colorFor(series.label, index);
			return {
				type: "bar",
				label: series.label,
				data: series.values,
				backgroundColor: color,
				borderRadius: 4,
				borderSkipped: false,
				maxBarThickness: 38,
				stack: "calls",
				order: 2
			};
		});

		datasets.push({
			type: "line",
			label: "Hồ sơ tương tác",
			data: data.dailyUniqueLoans,
			borderColor: "#181c32",
			backgroundColor: "#181c32",
			borderWidth: 2.5,
			borderDash: [6, 4],
			tension: 0.35,
			pointRadius: 3,
			pointHoverRadius: 6,
			pointBackgroundColor: "#ffffff",
			pointBorderWidth: 2,
			yAxisID: "yLoans",
			order: 1
		});

		new Chart(canvas, {
			data: { labels: data.dailyLabels, datasets: datasets },
			options: {
				interaction: { mode: "index", intersect: false },
				plugins: {
					legend: {
						position: "bottom",
						labels: { usePointStyle: true, pointStyle: "circle", padding: 16, font: { weight: "600" } }
					},
					tooltip: Object.assign(tooltipStyle(), {
						callbacks: {
							label: function (context) {
								return " " + context.dataset.label + ": " + formatNumber(context.parsed.y);
							},
							footer: function (items) {
								var total = items
									.filter(function (item) { return item.dataset.type === "bar"; })
									.reduce(function (sum, item) { return sum + item.parsed.y; }, 0);
								return "Tổng lượt gọi: " + formatNumber(total);
							}
						}
					})
				},
				scales: {
					x: { stacked: true, grid: { display: false }, ticks: { font: { weight: "600" } } },
					y: {
						stacked: true,
						beginAtZero: true,
						grid: { color: gridColor }, border: { display: false },
						ticks: { callback: formatNumber },
						title: { display: true, text: "Lượt gọi", font: { weight: "600" } }
					},
					yLoans: {
						position: "right",
						beginAtZero: true,
						grid: { display: false },
						ticks: { callback: formatNumber },
						title: { display: true, text: "Hồ sơ", font: { weight: "600" } }
					}
				}
			}
		});
	}

	var centerTextPlugin = {
		id: "cbCenterText",
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
			ctx.fillStyle = "#181c32";
			ctx.font = "700 22px " + Chart.defaults.font.family;
			ctx.fillText(options.text, x, y - 8);
			ctx.fillStyle = textColor;
			ctx.font = "600 12px " + Chart.defaults.font.family;
			ctx.fillText(options.subtext || "", x, y + 14);
			ctx.restore();
		}
	};

	function renderActionChart(data) {
		var canvas = document.getElementById("cbActionChart");
		if (!canvas || !data.actions.length) {
			return;
		}

		var total = data.actions.reduce(function (sum, item) { return sum + item.value; }, 0);
		var colors = data.actions.map(function (item, index) { return colorFor(item.label, index); });

		new Chart(canvas, {
			type: "doughnut",
			data: {
				labels: data.actions.map(function (item) { return item.label; }),
				datasets: [{
					data: data.actions.map(function (item) { return item.value; }),
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
					tooltip: Object.assign(tooltipStyle(), {
						callbacks: {
							label: function (context) {
								var share = total === 0 ? 0 : (context.parsed * 100 / total);
								return " " + formatNumber(context.parsed) + " lượt (" + share.toFixed(1) + "%)";
							}
						}
					}),
					cbCenterText: { text: formatNumber(total), subtext: "lượt gọi" }
				}
			},
			plugins: [centerTextPlugin]
		});

		var legend = document.getElementById("cbActionLegend");
		if (legend) {
			legend.innerHTML = "";
			data.actions.forEach(function (item, index) {
				var share = total === 0 ? 0 : (item.value * 100 / total);
				var li = document.createElement("li");
				var swatch = document.createElement("span");
				swatch.className = "cb-legend__swatch";
				swatch.style.background = colors[index];
				var label = document.createElement("span");
				label.className = "cb-legend__label";
				label.textContent = item.label;
				var value = document.createElement("span");
				value.className = "cb-legend__value";
				value.textContent = share.toFixed(1) + "%";
				li.appendChild(swatch);
				li.appendChild(label);
				li.appendChild(value);
				legend.appendChild(li);
			});
		}
	}

	function renderHourlyChart(data) {
		var canvas = document.getElementById("cbHourlyChart");
		if (!canvas) {
			return;
		}

		var max = Math.max.apply(null, data.hourlyTotals.concat([0]));
		new Chart(canvas, {
			type: "bar",
			data: {
				labels: data.hourlyTotals.map(function (_, hour) { return (hour < 10 ? "0" : "") + hour + "h"; }),
				datasets: [{
					data: data.hourlyTotals,
					backgroundColor: data.hourlyTotals.map(function (value) {
						return value === max && max > 0 ? "#f1416c" : "rgba(54, 153, 255, 0.55)";
					}),
					borderRadius: 3,
					maxBarThickness: 12
				}]
			},
			options: {
				plugins: {
					legend: { display: false },
					tooltip: Object.assign(tooltipStyle(), {
						callbacks: {
							label: function (context) { return " " + formatNumber(context.parsed.y) + " lượt gọi"; }
						}
					})
				},
				scales: {
					x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 8, font: { size: 10 } } },
					y: { display: false, beginAtZero: true }
				}
			}
		});
	}

	function renderHorizontalBar(canvasId, items, color) {
		var canvas = document.getElementById(canvasId);
		if (!canvas || !items.length) {
			return;
		}

		var total = items.reduce(function (sum, item) { return sum + item.value; }, 0);
		new Chart(canvas, {
			type: "bar",
			data: {
				labels: items.map(function (item) { return item.label; }),
				datasets: [{
					data: items.map(function (item) { return item.value; }),
					backgroundColor: items.map(function (item) { return item.label === "Khác" ? otherColor : color; }),
					borderRadius: 6,
					borderSkipped: false,
					barThickness: 18
				}]
			},
			options: {
				indexAxis: "y",
				plugins: {
					legend: { display: false },
					tooltip: Object.assign(tooltipStyle(), {
						callbacks: {
							label: function (context) {
								var share = total === 0 ? 0 : (context.parsed.x * 100 / total);
								return " " + formatNumber(context.parsed.x) + " lượt (" + share.toFixed(1) + "%)";
							}
						}
					})
				},
				scales: {
					x: { beginAtZero: true, grid: { color: gridColor }, ticks: { callback: formatNumber, maxTicksLimit: 5 } },
					y: {
						grid: { display: false },
						ticks: {
							font: { weight: "600" },
							callback: function (value) {
								var label = this.getLabelForValue(value);
								return label.length > 22 ? label.slice(0, 21) + "…" : label;
							}
						}
					}
				}
			}
		});
	}

	document.addEventListener("DOMContentLoaded", function () {
		var dataElement = document.getElementById("customerBehaviorData");
		if (!dataElement || typeof Chart === "undefined") {
			return;
		}

		var data = JSON.parse(dataElement.textContent || "{}");
		applyDefaults();
		renderSparklines(data);
		renderDailyChart(data);
		renderActionChart(data);
		renderHourlyChart(data);
		renderHorizontalBar("cbProductChart", data.products || [], "#3699ff");
		renderHorizontalBar("cbStatusChart", data.statuses || [], "#7239ea");
		renderHorizontalBar("cbPartnerChart", data.partners || [], "#0f766e");
	});
})();
