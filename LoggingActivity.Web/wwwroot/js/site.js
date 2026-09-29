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

			// Quyền đang tick sẵn mà không đến từ nhóm là quyền riêng: giữ nguyên khi đồng bộ theo nhóm.
			var initialRequired = getRequiredPermissionCodes();
			permissionCheckboxes.forEach(function (checkbox) {
				if (checkbox.checked && !initialRequired.has(checkbox.value)) {
					manuallyTouchedPermissions.add(checkbox.value);
				}
			});

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

	// Khối code trong view bị lẫn khoảng trắng thụt lề của file Razor: bỏ phần thụt lề chung của các dòng sau dòng đầu.
	function initCodeBlockDedent() {
		document.querySelectorAll("pre.api-block > code").forEach(function (code) {
			var lines = code.textContent.split("\n");
			if (lines.length < 2) {
				return;
			}

			var rest = lines.slice(1);
			var indents = rest
				.filter(function (line) { return line.trim().length > 0; })
				.map(function (line) { return line.match(/^ */)[0].length; });
			if (!indents.length) {
				return;
			}

			var common = Math.min.apply(null, indents);
			var continuation = /^curl\b/.test(lines[0].trim()) ? "    " : "";
			if (common === 0 && !continuation) {
				return;
			}

			code.textContent = [lines[0]].concat(rest.map(function (line) {
				return line.trim().length ? continuation + line.slice(common) : "";
			})).join("\n");
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
		runInitializer("initEnhancedSelects", initEnhancedSelects);
		runInitializer("initAutoSubmitSelectForms", initAutoSubmitSelectForms);
		runInitializer("initUserPermissionSyncForms", initUserPermissionSyncForms);
		runInitializer("initActorLogModal", initActorLogModal);
		runInitializer("initLogsInsightsAsync", initLogsInsightsAsync);
		runInitializer("initListLoadingState", initListLoadingState);
		runInitializer("initCodeBlockDedent", initCodeBlockDedent);
	});
})();
