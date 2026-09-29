(function () {
	"use strict";

	var IC = window.InsightCharts;

	// Nhãn series dạng "Partner - Action": rút gọn phần partner khi quá dài để legend gọn hơn.
	function shortSeriesLabel(label) {
		return label.length > 34 ? label.slice(0, 33) + "…" : label;
	}

	function renderSparklines(data) {
		document.querySelectorAll("[data-dashboard-spark]").forEach(function (canvas) {
			var key = canvas.getAttribute("data-dashboard-spark");
			var labels = key === "hourlyValues" ? data.hourlyLabels : data.dailyLabels;
			IC.renderSparkline(canvas, labels, data[key] || [], canvas.getAttribute("data-color") || IC.palette[0]);
		});
	}

	function renderTrendChart(data) {
		var canvas = document.getElementById("dashboardTrendChart");
		if (!canvas) {
			return;
		}

		var datasets = data.dailySeries.map(function (series, index) {
			return {
				type: "bar",
				label: series.label,
				data: series.values,
				backgroundColor: IC.colorFor(series.label, index),
				borderRadius: 4,
				borderSkipped: false,
				maxBarThickness: 46,
				stack: "logs",
				order: 2
			};
		});

		datasets.push({
			type: "line",
			label: "Tổng log",
			data: data.dailyTotals,
			borderColor: "#181c32",
			borderWidth: 2,
			borderDash: [6, 4],
			tension: 0.35,
			pointRadius: 3,
			pointHoverRadius: 6,
			pointBackgroundColor: "#ffffff",
			pointBorderColor: "#181c32",
			pointBorderWidth: 2,
			fill: false,
			order: 1
		});

		new Chart(canvas, {
			data: { labels: data.dailyLabels, datasets: datasets },
			options: {
				interaction: { mode: "index", intersect: false },
				plugins: {
					legend: {
						position: "bottom",
						labels: {
							usePointStyle: true,
							pointStyle: "circle",
							padding: 16,
							font: { weight: "600" },
							generateLabels: function (chart) {
								return Chart.defaults.plugins.legend.labels.generateLabels(chart).map(function (item) {
									item.text = shortSeriesLabel(item.text);
									return item;
								});
							}
						}
					},
					tooltip: IC.tooltip({
						filter: function (item) { return item.dataset.type === "bar" && item.parsed.y > 0; },
						itemSort: function (a, b) { return b.parsed.y - a.parsed.y; },
						callbacks: {
							title: function (items) { return items.length ? "Ngày " + items[0].label : ""; },
							label: function (context) { return " " + context.dataset.label + ": " + IC.formatNumber(context.parsed.y) + " log"; },
							footer: function (items) {
								if (!items.length) {
									return "";
								}
								return "Tổng: " + IC.formatNumber(data.dailyTotals[items[0].dataIndex]) + " log";
							}
						}
					})
				},
				scales: {
					x: { stacked: true, grid: { display: false }, ticks: { font: { weight: "600" } } },
					y: {
						stacked: true,
						beginAtZero: true,
						grid: { color: IC.gridColor },
						border: { display: false },
						ticks: { callback: IC.formatNumber, precision: 0 }
					}
				}
			}
		});
	}

	function renderHourlyChart(data) {
		var canvas = document.getElementById("dashboardHourlyChart");
		if (!canvas) {
			return;
		}

		var values = data.hourlyValues;
		var max = Math.max.apply(null, values.concat([0]));
		var currentHour = new Date().getHours();

		new Chart(canvas, {
			type: "bar",
			data: {
				labels: data.hourlyLabels,
				datasets: [{
					label: "Số log",
					data: values,
					backgroundColor: function (context) {
						var value = values[context.dataIndex];
						if (value === max && max > 0) {
							return "#f1416c";
						}

						return IC.verticalGradient(context, "#3699ff", "d9");
					},
					hoverBackgroundColor: "#187de4",
					borderRadius: 6,
					borderSkipped: false,
					maxBarThickness: 26
				}]
			},
			options: {
				plugins: {
					legend: { display: false },
					tooltip: IC.tooltip({
						callbacks: {
							title: function (items) {
								if (!items.length) {
									return "";
								}
								var hour = items[0].label.slice(0, 2);
								return hour + ":00 - " + hour + ":59";
							},
							label: function (context) { return " " + IC.formatNumber(context.parsed.y) + " log"; },
							afterLabel: function (context) {
								return context.parsed.y === max && max > 0 ? "Giờ cao điểm" : "";
							}
						}
					}),
					insightReferenceLine: {
						value: data.hourlyAverage,
						label: "TB " + IC.formatNumber(Math.round(data.hourlyAverage)) + "/giờ",
						color: "#d97706"
					}
				},
				scales: {
					x: {
						grid: { display: false },
						ticks: {
							maxRotation: 0,
							autoSkip: false,
							font: function (context) {
								return { size: 11, weight: context.index === currentHour ? "800" : "500" };
							},
							callback: function (value, index) { return index % 2 === 0 ? this.getLabelForValue(value).slice(0, 2) + "h" : ""; }
						}
					},
					y: {
						beginAtZero: true,
						grid: { color: IC.gridColor },
						border: { display: false },
						ticks: { callback: IC.formatNumber, precision: 0, maxTicksLimit: 6 }
					}
				}
			},
			plugins: [IC.referenceLinePlugin]
		});
	}

	function renderTopUsersChart(data) {
		var canvas = document.getElementById("dashboardTopUsersChart");
		if (!canvas || !data.topUsers.length) {
			return;
		}

		new Chart(canvas, {
			type: "bar",
			data: {
				labels: data.topUsers.map(function (item) { return item.label; }),
				datasets: [{
					label: "Số action",
					data: data.topUsers.map(function (item) { return item.value; }),
					backgroundColor: function (context) {
						return context.dataIndex === 0
							? IC.horizontalGradient(context, "#f1416c", "#ff8fab")
							: IC.horizontalGradient(context, "#3699ff", "#7239ea");
					},
					borderRadius: 8,
					borderSkipped: false,
					barThickness: 22
				}]
			},
			options: {
				indexAxis: "y",
				plugins: {
					legend: { display: false },
					tooltip: IC.tooltip({
						callbacks: {
							label: function (context) { return " " + IC.formatNumber(context.parsed.x) + " action"; },
							afterLabel: function (context) {
								var item = data.topUsers[context.dataIndex];
								return "Nhiều nhất: " + item.topAction + " (" + IC.formatNumber(item.topActionCount) + ")";
							}
						}
					})
				},
				scales: {
					x: { beginAtZero: true, grid: { color: IC.gridColor }, border: { display: false }, ticks: { callback: IC.formatNumber, precision: 0, maxTicksLimit: 6 } },
					y: {
						grid: { display: false },
						ticks: {
							font: { weight: "600" },
							callback: function (value) {
								var label = this.getLabelForValue(value);
								return label.length > 24 ? label.slice(0, 23) + "…" : label;
							}
						}
					}
				}
			}
		});
	}

	IC && IC.whenReady(function () {
		var dataElement = document.getElementById("dashboardData");
		if (!dataElement || typeof Chart === "undefined" || !IC) {
			return;
		}

		var data = JSON.parse(dataElement.textContent || "{}");
		IC.applyDefaults();
		renderSparklines(data);
		renderTrendChart(data);

		var actionCanvas = document.getElementById("dashboardActionChart");
		if (actionCanvas && data.actionShares.length) {
			IC.renderDoughnut(actionCanvas, data.actionShares, {
				unit: "log",
				centerSubtext: "log 7 ngày",
				legendElement: document.getElementById("dashboardActionLegend")
			});
		}

		renderHourlyChart(data);
		renderTopUsersChart(data);
	});
})();
