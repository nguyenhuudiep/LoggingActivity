(function () {
	"use strict";

	var IC = window.InsightCharts;

	function renderSparklines(data) {
		document.querySelectorAll("[data-cb-spark]").forEach(function (canvas) {
			IC.renderSparkline(
				canvas,
				data.dailyLabels,
				data[canvas.getAttribute("data-cb-spark")] || [],
				canvas.getAttribute("data-cb-color") || IC.palette[0]);
		});
	}

	function renderDailyChart(data) {
		var canvas = document.getElementById("cbDailyChart");
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
					tooltip: IC.tooltip({
						callbacks: {
							label: function (context) {
								return " " + context.dataset.label + ": " + IC.formatNumber(context.parsed.y);
							},
							footer: function (items) {
								var total = items
									.filter(function (item) { return item.dataset.type === "bar"; })
									.reduce(function (sum, item) { return sum + item.parsed.y; }, 0);
								return "Tổng lượt gọi: " + IC.formatNumber(total);
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
						ticks: { callback: IC.formatNumber },
						title: { display: true, text: "Lượt gọi", font: { weight: "600" } }
					},
					yLoans: {
						position: "right",
						beginAtZero: true,
						grid: { display: false },
						ticks: { callback: IC.formatNumber },
						title: { display: true, text: "Hồ sơ", font: { weight: "600" } }
					}
				}
			}
		});
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
					tooltip: IC.tooltip({
						callbacks: {
							label: function (context) { return " " + IC.formatNumber(context.parsed.y) + " lượt gọi"; }
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
					backgroundColor: items.map(function (item) { return item.label === IC.otherLabel ? IC.otherColor : color; }),
					borderRadius: 6,
					borderSkipped: false,
					barThickness: 18
				}]
			},
			options: {
				indexAxis: "y",
				plugins: {
					legend: { display: false },
					tooltip: IC.tooltip({
						callbacks: {
							label: function (context) {
								var share = total === 0 ? 0 : (context.parsed.x * 100 / total);
								return " " + IC.formatNumber(context.parsed.x) + " lượt (" + share.toFixed(1) + "%)";
							}
						}
					})
				},
				scales: {
					x: { beginAtZero: true, grid: { color: IC.gridColor }, ticks: { callback: IC.formatNumber, maxTicksLimit: 5 } },
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

	IC && IC.whenReady(function () {
		var dataElement = document.getElementById("customerBehaviorData");
		if (!dataElement || typeof Chart === "undefined" || !IC) {
			return;
		}

		var data = JSON.parse(dataElement.textContent || "{}");
		IC.applyDefaults();
		renderSparklines(data);
		renderDailyChart(data);

		var actionCanvas = document.getElementById("cbActionChart");
		if (actionCanvas && data.actions.length) {
			IC.renderDoughnut(actionCanvas, data.actions, {
				unit: "lượt",
				centerSubtext: "lượt gọi",
				legendElement: document.getElementById("cbActionLegend")
			});
		}

		renderHourlyChart(data);
		renderHorizontalBar("cbProductChart", data.products || [], "#3699ff");
		renderHorizontalBar("cbStatusChart", data.statuses || [], "#7239ea");
		renderHorizontalBar("cbPartnerChart", data.partners || [], "#0f766e");
	});
})();
