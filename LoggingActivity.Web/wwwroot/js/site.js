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
		var numberFormatter = new Intl.NumberFormat("vi-VN");

		function formatTooltipSeriesName(rawName) {
			if (!rawName) {
				return "Series";
			}

			var parts = rawName.split("-").map(function (item) { return item.trim(); }).filter(Boolean);
			return parts.length > 1 ? parts[parts.length - 1] : rawName;
		}

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
								pointHoverRadius: 6,
								pointHitRadius: 14
								};
							})
						},
						options: {
							interaction: {
								mode: "index",
								intersect: false
							},
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
									displayColors: true,
									usePointStyle: true,
									bodySpacing: 8,
									boxPadding: 4,
									titleFont: {
										size: 13,
										weight: "700"
									},
									bodyFont: {
										size: 12,
										weight: "600"
									},
									cornerRadius: 12,
									padding: {
										top: 12,
										right: 14,
										bottom: 12,
										left: 14
									},
									callbacks: {
										title: function (items) {
											if (!items || items.length === 0) {
												return "";
											}
											return "Ngày " + items[0].label;
										},
										label: function (context) {
											var name = formatTooltipSeriesName(context.dataset.label || "Series");
											var value = typeof context.parsed.y === "number" ? context.parsed.y : 0;
											return name + ": " + numberFormatter.format(value) + " log";
										},
										afterBody: function (items) {
											if (!items || items.length === 0) {
												return [];
											}

											var total = items.reduce(function (sum, item) {
												var value = typeof item.parsed.y === "number" ? item.parsed.y : 0;
												return sum + value;
											}, 0);

											return ["Tổng tại ngày: " + numberFormatter.format(total) + " log"];
										}
									}
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

		var hourlyTrendHost = document.querySelector("[data-hourly-trend-chart]");
		if (hourlyTrendHost && typeof Chart !== "undefined") {
			var hourlyTrendCanvas = document.getElementById("hourlyTrendChart");
			if (hourlyTrendCanvas) {
				var hourlyLabels = JSON.parse(hourlyTrendHost.getAttribute("data-hourly-labels") || "[]");
				var hourlyValues = JSON.parse(hourlyTrendHost.getAttribute("data-hourly-values") || "[]");

				if (hourlyLabels.length > 0 && hourlyValues.length > 0) {
					new Chart(hourlyTrendCanvas, {
						type: "line",
						data: {
							labels: hourlyLabels,
							datasets: [{
								label: "Số log",
								data: hourlyValues,
								borderColor: "#0f766e",
								backgroundColor: "rgba(15, 118, 110, 0.18)",
								fill: true,
								borderWidth: 3,
								tension: 0.3,
								pointRadius: 2,
								pointHoverRadius: 5
							}]
						},
						options: {
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
							responsive: true,
							scales: {
								x: {
									grid: { color: "rgba(148, 163, 184, 0.12)" },
									ticks: { color: "#6a7280", maxRotation: 0, autoSkip: true, maxTicksLimit: 12 }
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

		var topUsersTodayHost = document.querySelector("[data-top-users-today-chart]");
		if (topUsersTodayHost && typeof Chart !== "undefined") {
			var topUsersCanvas = document.getElementById("topUsersTodayChart");
			if (topUsersCanvas) {
				var userLabels = JSON.parse(topUsersTodayHost.getAttribute("data-user-labels") || "[]");
				var userValues = JSON.parse(topUsersTodayHost.getAttribute("data-user-values") || "[]");
				var userTopActions = JSON.parse(topUsersTodayHost.getAttribute("data-user-top-actions") || "[]");

				if (userLabels.length > 0 && userValues.length > 0) {
					new Chart(topUsersCanvas, {
						type: "bar",
						data: {
							labels: userLabels,
							datasets: [{
								label: "Số action",
								data: userValues,
								backgroundColor: [
									"rgba(185, 28, 28, 0.88)",
									"rgba(220, 38, 38, 0.82)",
									"rgba(239, 68, 68, 0.76)",
									"rgba(248, 113, 113, 0.72)",
									"rgba(252, 165, 165, 0.68)"
								],
								borderColor: "rgba(127, 29, 29, 0.9)",
								borderWidth: 1,
								borderRadius: 10,
								borderSkipped: false
							}]
						},
						options: {
							indexAxis: "y",
							plugins: {
								legend: { display: false },
								tooltip: {
									backgroundColor: "rgba(17, 24, 39, 0.92)",
									titleColor: "#f9fafb",
									bodyColor: "#f9fafb",
									cornerRadius: 12,
									padding: 12,
									callbacks: {
										title: function (items) {
											if (!items || items.length === 0) {
												return "";
											}

											var index = items[0].dataIndex;
											return userLabels[index] || "User";
										},
										label: function (context) {
											var value = typeof context.parsed.x === "number" ? context.parsed.x : 0;
											return "Số action: " + numberFormatter.format(value);
										},
										afterLabel: function (context) {
											var topAction = userTopActions[context.dataIndex] || "N/A";
											return "Action cao nhất: " + topAction;
										}
									}
								}
							},
							maintainAspectRatio: false,
							responsive: true,
							scales: {
								x: {
									beginAtZero: true,
									grid: { color: "rgba(148, 163, 184, 0.18)" },
									ticks: { color: "#6a7280", precision: 0 }
								},
								y: {
									grid: { display: false },
									ticks: { color: "#6a7280", font: { weight: 600 } }
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

	function initUserPermissionSyncForms() {
		var forms = document.querySelectorAll("form[data-user-permission-sync]");
		if (!forms.length) {
			return;
		}

		forms.forEach(function (form) {
			var groupCheckboxes = Array.from(form.querySelectorAll('input[name="SelectedPermissionGroupIds"]'));
			var permissionCheckboxes = Array.from(form.querySelectorAll('input[name="SelectedPermissions"]'));
			if (!groupCheckboxes.length || !permissionCheckboxes.length) {
				return;
			}

			var permissionMap = new Map();
			permissionCheckboxes.forEach(function (checkbox) {
				permissionMap.set(checkbox.value, checkbox);
			});

			var manuallyTouchedPermissions = new Set();

			function getRequiredPermissionCodes() {
				var required = new Set();

				groupCheckboxes.forEach(function (checkbox) {
					if (!checkbox.checked) {
						return;
					}

					var codes = (checkbox.getAttribute("data-permissions") || "")
						.split(",")
						.map(function (code) { return code.trim(); })
						.filter(Boolean);

					codes.forEach(function (code) {
						required.add(code);
					});
				});

				return required;
			}

			function syncPermissions() {
				var requiredPermissionCodes = getRequiredPermissionCodes();

				permissionMap.forEach(function (checkbox, code) {
					if (manuallyTouchedPermissions.has(code)) {
						return;
					}

					checkbox.checked = requiredPermissionCodes.has(code);
				});
			}

			groupCheckboxes.forEach(function (checkbox) {
				checkbox.addEventListener("change", function () {
					syncPermissions();
				});
			});

			permissionCheckboxes.forEach(function (checkbox) {
				checkbox.addEventListener("change", function () {
					manuallyTouchedPermissions.add(checkbox.value);
				});
			});

			syncPermissions();
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

	function initActorLogModal() {
		var modalElement = document.getElementById("actorLogDetailsModal");
		if (!modalElement || typeof window.bootstrap === "undefined") {
			return;
		}

		var modalContent = modalElement.querySelector("[data-actor-log-modal-content]");
		if (!modalContent) {
			return;
		}

		var modal = new window.bootstrap.Modal(modalElement);
		var loadingMarkup = modalContent.innerHTML;

		function setLoadingState() {
			modalContent.innerHTML = loadingMarkup;
		}

		function loadModalContent(url, shouldOpen) {
			setLoadingState();

			fetch(url, {
				headers: {
					"X-Requested-With": "XMLHttpRequest"
				}
			})
				.then(function (response) {
					if (!response.ok) {
						throw new Error("Không thể tải dữ liệu chi tiết theo key.");
					}

					return response.text();
				})
				.then(function (html) {
					modalContent.innerHTML = html;
					if (shouldOpen) {
						modal.show();
					}
				})
				.catch(function () {
					modalContent.innerHTML = [
						'<div class="modal-header">',
						'  <div>',
						'    <div class="danger-modal__eyebrow">Chi tiết theo key</div>',
						'    <h2 class="modal-title fs-5 fw-bold mb-0">Không tải được dữ liệu</h2>',
						'  </div>',
						'  <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng"></button>',
						'</div>',
						'<div class="modal-body text-secondary">Không thể tải danh sách bản ghi cho key này. Hãy thử lại.</div>'
					].join("");
					if (shouldOpen) {
						modal.show();
					}
				});
		}

		document.addEventListener("click", function (event) {
			var trigger = event.target.closest("[data-actor-log-trigger], [data-actor-log-modal-link]");
			if (!trigger) {
				return;
			}

			var url = trigger.getAttribute("href");
			if (!url) {
				return;
			}

			event.preventDefault();
			loadModalContent(url, trigger.hasAttribute("data-actor-log-trigger"));
		});
	}

	function initLogsInsightsAsync() {
		var host = document.querySelector("[data-logs-insights-url]");
		if (!host) {
			return;
		}

		var url = host.getAttribute("data-logs-insights-url");
		if (!url) {
			return;
		}

		var maxRetries = 3;
		var retryDelay = 500; // milliseconds

		function fetchWithRetry(attempt) {
			if (attempt === undefined) {
				attempt = 0;
			}

			fetch(url, {
				headers: {
					"X-Requested-With": "XMLHttpRequest"
				}
			})
				.then(function (response) {
					if (!response.ok) {
						throw new Error("HTTP " + response.status);
					}

					return response.text();
				})
				.then(function (html) {
					host.innerHTML = html;
				})
				.catch(function (error) {
					if (attempt < maxRetries) {
						var delay = retryDelay * Math.pow(2, attempt);
						console.log("Retry tải thống kê (lần " + (attempt + 1) + "/" + maxRetries + ") sau " + delay + "ms");
						setTimeout(function () {
							fetchWithRetry(attempt + 1);
						}, delay);
					} else {
						console.error("Lỗi tải thống kê sau " + maxRetries + " lần retry:", error);
						host.innerHTML = '<div class="alert alert-warning" role="alert">Không thể tải thống kê và cảnh báo. <a href="javascript:location.reload()">Tải lại trang</a></div>';
					}
				});
		}

		fetchWithRetry();
	}

	function initListLoadingState() {
		var body = document.body;
		if (!body) {
			return;
		}
		var minVisibleDurationMs = 300;
		var loadingIndicatorDelayMs = 160;
		var loadingStartedAt = Date.now();
		var showLoadingTimerId = null;

		function showLoading() {
			if (showLoadingTimerId !== null) {
				window.clearTimeout(showLoadingTimerId);
				showLoadingTimerId = null;
			}

			body.classList.add("list-page-loading");
			body.classList.remove("list-page-ready");
			loadingStartedAt = Date.now();
		}

		function queueLoading() {
			if (showLoadingTimerId !== null) {
				return;
			}

			showLoadingTimerId = window.setTimeout(function () {
				showLoadingTimerId = null;
				showLoading();
			}, loadingIndicatorDelayMs);
		}

		function hideLoadingWithMinimumDuration() {
			var elapsed = Date.now() - loadingStartedAt;
			var wait = Math.max(0, minVisibleDurationMs - elapsed);
			window.setTimeout(function () {
				body.classList.remove("list-page-loading");
				body.classList.add("list-page-ready");
			}, wait);
		}

		document.querySelectorAll("a[data-show-loading-nav='true']").forEach(function (link) {
			link.addEventListener("click", function (event) {
				if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
					return;
				}

				var href = link.getAttribute("href");
				if (!href || href.startsWith("#") || href.startsWith("javascript:")) {
					return;
				}

				queueLoading();
			});
		});

		if (body.getAttribute("data-list-page") !== "true") {
			body.classList.add("list-page-ready");
			return;
		}

		body.classList.add("list-page-loading");
		if (document.readyState === "complete") {
			hideLoadingWithMinimumDuration();
		} else {
			window.addEventListener("load", hideLoadingWithMinimumDuration, { once: true });
		}

		window.addEventListener("beforeunload", function () {
			body.classList.add("list-page-loading");
			body.classList.remove("list-page-ready");
		});

		function maybeShowLoadingForLink(link) {
			if (!link || link.target === "_blank" || link.hasAttribute("download")) {
				return;
			}

			if (link.hasAttribute("data-actor-log-trigger") || link.hasAttribute("data-actor-log-modal-link")) {
				return;
			}

			var href = link.getAttribute("href");
			if (!href || href.startsWith("#") || href.startsWith("javascript:")) {
				return;
			}

			var url;
			try {
				url = new URL(href, window.location.origin);
			} catch (_) {
				return;
			}

			if (url.origin !== window.location.origin) {
				return;
			}

			var samePath = url.pathname === window.location.pathname;
			var hasPageQuery = url.searchParams.has("page") || url.searchParams.has("pagesize");
			if (samePath || hasPageQuery) {
				queueLoading();
				return true;
			}

			return false;
		}

		document.querySelectorAll("form[method='get'], form:not([method])").forEach(function (form) {
			form.addEventListener("submit", function (event) {
				if (form.dataset.loadingSubmitInProgress === "true") {
					return;
				}

				form.dataset.loadingSubmitInProgress = "true";
				queueLoading();
			});
		});

		document.addEventListener("click", function (event) {
			if (event.defaultPrevented) {
				return;
			}

			var link = event.target.closest("a");
			maybeShowLoadingForLink(link);
		});
	}

	function runInitializer(name, initFn) {
		try {
			initFn();
		} catch (error) {
			if (typeof console !== "undefined" && typeof console.error === "function") {
				console.error("[site.js] initializer failed:", name, error);
			}
		}
	}

	document.addEventListener("DOMContentLoaded", function () {
		runInitializer("initSidebarToggle", initSidebarToggle);
		runInitializer("closeMobileNavOnLinkClick", closeMobileNavOnLinkClick);
		runInitializer("initLogCharts", initLogCharts);
		runInitializer("initEnhancedSelects", initEnhancedSelects);
		runInitializer("initAutoSubmitSelectForms", initAutoSubmitSelectForms);
		runInitializer("initUserPermissionSyncForms", initUserPermissionSyncForms);
		runInitializer("initActorLogModal", initActorLogModal);
		runInitializer("initLogsInsightsAsync", initLogsInsightsAsync);
		runInitializer("initListLoadingState", initListLoadingState);
	});
})();
