(function () {
	function initSidebarToggle() {
		var toggleButton = document.getElementById("sidebarToggle");
		if (!toggleButton) {
			return;
		}

		toggleButton.addEventListener("click", function () {
			document.body.classList.toggle("sidebar-open");
		});
	}

	function closeMobileNavOnLinkClick() {
		var sidebar = document.getElementById("adminSidebar");
		if (!sidebar) {
			return;
		}

		var navLinks = sidebar.querySelectorAll(".sidebar-link");

		navLinks.forEach(function (link) {
			link.addEventListener("click", function () {
				if (window.innerWidth < 1200) {
					document.body.classList.remove("sidebar-open");
				}
			});
		});
	}

	function initLogCharts() {
		var actionTrendHost = document.querySelector("[data-action-trend-chart]");
		if (actionTrendHost && typeof Chart !== "undefined") {
			var actionTrendCanvas = document.getElementById("actionTrendChart");
			if (actionTrendCanvas) {
				var actionTrendLabels = JSON.parse(actionTrendHost.getAttribute("data-chart-labels") || "[]");
				var actionTrendDatasets = JSON.parse(actionTrendHost.getAttribute("data-chart-datasets") || "[]");
				var palette = [
					"#3699ff",
					"#0f766e",
					"#d97706",
					"#7239ea",
					"#f1416c",
					"#15803d",
					"#334155"
				];

				if (actionTrendLabels.length > 0 && actionTrendDatasets.length > 0) {
					new Chart(actionTrendCanvas, {
						type: "line",
						data: {
							labels: actionTrendLabels,
							datasets: actionTrendDatasets.map(function (dataset, index) {
								var color = palette[index % palette.length];
								return {
									label: dataset.label,
									data: dataset.values,
									borderColor: color,
									backgroundColor: color + "22",
									fill: false,
									borderWidth: 3,
									tension: 0.35,
									pointRadius: 4,
									pointHoverRadius: 6
								};
							})
						},
						options: {
							plugins: {
								legend: {
									position: "bottom",
									labels: {
										usePointStyle: true,
										pointStyle: "circle",
										padding: 18,
										color: "#344150",
										font: { weight: 700 }
									}
								},
								tooltip: {
									backgroundColor: "rgba(17, 24, 39, 0.92)",
									titleColor: "#f9fafb",
									bodyColor: "#f9fafb",
									cornerRadius: 12,
									padding: 12
								}
							},
							maintainAspectRatio: false,
							responsive: true,
							scales: {
								x: {
									grid: { display: false },
									ticks: { color: "#6a7280", font: { weight: 700 } }
								},
								y: {
									beginAtZero: true,
									grid: { color: "rgba(148, 163, 184, 0.18)" },
									ticks: { color: "#6a7280", precision: 0 }
								}
							}
						}
					});
				}
			}
		}

		var chartHost = document.querySelector("[data-log-charts]");
		if (!chartHost || typeof Chart === "undefined") {
			return;
		}

		var dailyCanvas = document.getElementById("dailyActivityChart");
		var actionsCanvas = document.getElementById("topActionsChart");
		if (!dailyCanvas || !actionsCanvas) {
			return;
		}

		var dailyLabels = JSON.parse(chartHost.getAttribute("data-daily-labels") || "[]");
		var dailyValues = JSON.parse(chartHost.getAttribute("data-daily-values") || "[]");
		var topActionLabels = JSON.parse(chartHost.getAttribute("data-action-labels") || "[]");
		var topActionValues = JSON.parse(chartHost.getAttribute("data-action-values") || "[]");

		var sharedOptions = {
			plugins: {
				legend: { display: false },
				tooltip: {
					backgroundColor: "rgba(17, 24, 39, 0.92)",
					titleColor: "#f9fafb",
					bodyColor: "#f9fafb",
					cornerRadius: 12,
					padding: 12
				}
			},
			maintainAspectRatio: false,
			responsive: true
		};

		if (dailyLabels.length > 0 && dailyValues.length > 0) {
			new Chart(dailyCanvas, {
				type: "bar",
				data: {
					labels: dailyLabels,
					datasets: [{
						data: dailyValues,
						borderRadius: 12,
						borderSkipped: false,
						backgroundColor: [
							"rgba(15, 118, 110, 0.92)",
							"rgba(15, 118, 110, 0.88)",
							"rgba(15, 118, 110, 0.84)",
							"rgba(15, 118, 110, 0.8)",
							"rgba(15, 118, 110, 0.76)",
							"rgba(15, 118, 110, 0.72)",
							"rgba(15, 118, 110, 0.68)"
						],
						hoverBackgroundColor: "rgba(19, 78, 74, 1)"
					}]
				},
				options: {
					...sharedOptions,
					scales: {
						x: {
							grid: { display: false },
							ticks: { color: "#6a7280", font: { weight: 700 } }
						},
						y: {
							beginAtZero: true,
							grid: { color: "rgba(148, 163, 184, 0.18)" },
							ticks: {
								color: "#6a7280",
								precision: 0
							}
						}
					}
				}
			});
		}

		if (topActionLabels.length > 0 && topActionValues.length > 0) {
			new Chart(actionsCanvas, {
				type: "doughnut",
				data: {
					labels: topActionLabels,
					datasets: [{
						data: topActionValues,
						backgroundColor: [
							"#0f766e",
							"#d97706",
							"#334155",
							"#15803d",
							"#b45309",
							"#0f766e99"
						],
						borderWidth: 0,
						hoverOffset: 8
					}]
				},
				options: {
					...sharedOptions,
					cutout: "68%",
					plugins: {
						...sharedOptions.plugins,
						legend: {
							position: "bottom",
							labels: {
								usePointStyle: true,
								pointStyle: "circle",
								padding: 18,
								color: "#344150",
								font: { weight: 700 }
							}
						}
					}
				}
			});
		}
	}

	function initAutoSubmitSelectForms() {
		var forms = document.querySelectorAll("form[data-auto-submit-selects]");
		if (!forms.length) {
			return;
		}

		function submitForm(form) {
			if (typeof form.requestSubmit === "function") {
				form.requestSubmit();
				return;
			}

			form.submit();
		}

		forms.forEach(function (form) {
			var selects = form.querySelectorAll("select");

			selects.forEach(function (select) {
				select.addEventListener("change", function () {
					submitForm(form);
				});
			});
		});

		if (typeof window.jQuery === "undefined") {
			return;
		}

		window.jQuery("form[data-auto-submit-selects] select[data-enhanced-select]").each(function () {
			var select = window.jQuery(this);
			var form = this.form;

			if (!form) {
				return;
			}

			select.on("select2:select select2:clear", function () {
				submitForm(form);
			});
		});
	}

	function initEnhancedSelects() {
		if (typeof window.jQuery === "undefined" || typeof window.jQuery.fn.select2 === "undefined") {
			return;
		}

		window.jQuery("select[data-enhanced-select]").each(function () {
			var element = window.jQuery(this);
			if (element.hasClass("select2-hidden-accessible")) {
				return;
			}

			var hideSearch = element.data("hideSearch") === true || element.attr("data-hide-search") === "true";
			var placeholder = element.attr("data-placeholder") || "Chọn giá trị";

			element.select2({
				theme: "bootstrap-5",
				width: "100%",
				placeholder: placeholder,
				minimumResultsForSearch: hideSearch ? Infinity : 0,
				selectionCssClass: "select2-selection--admin",
				dropdownCssClass: "select2-dropdown--admin"
			});
		});
	}

	document.addEventListener("DOMContentLoaded", function () {
		initSidebarToggle();
		closeMobileNavOnLinkClick();
		initLogCharts();
		initEnhancedSelects();
		initAutoSubmitSelectForms();
	});
})();
