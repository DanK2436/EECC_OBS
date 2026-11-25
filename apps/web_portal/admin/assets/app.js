/* ==============================================================================
   EECC Admin — Application JS (Vanilla)
   Connexion Supabase Auth + lecture des dons + gestion des retraits (OTP)
   ============================================================================== */

// Configuration Supabase (clés publiques — côté client uniquement)
const SUPABASE_URL = "https://rwojdiomftillhsamjal.supabase.co";
const SUPABASE_ANON_KEY = "sb_publishable_GjqG64b82uXaDQzGJmo3Aw_pIcpjxKb";

const API_HEADERS = {
  apikey: SUPABASE_ANON_KEY,
  Authorization: `Bearer ${SUPABASE_ANON_KEY}`,
  "Content-Type": "application/json",
};

let currentAccessToken = null;
let otpTimerInterval = null;
let otpTimeLeft = 0;
let currentWithdrawalId = null;

/* -------------------------------------------------------------
   État de l'application
   ------------------------------------------------------------- */
const app = {
  isAuthenticated: false,

  init() {
    this.bindNavigation();
    this.bindLogout();
    this.restoreSession();

    // Si on est sur la page de login
    const loginForm = document.getElementById("login-form");
    if (loginForm) {
      loginForm.addEventListener("submit", (e) => this.handleLogin(e));
    }

    // Si on est sur le dashboard / trésorerie
    if (document.getElementById("donations-tbody")) {
      this.loadDashboardData();
    }

    // Formulaire de création de retrait
    const wdForm = document.getElementById("withdrawal-form");
    if (wdForm) {
      wdForm.addEventListener("submit", (e) => this.handleCreateWithdrawal(e));

      // Calcul automatique du net quand le brut ou les frais changent
      document.getElementById("wd-amount-gross").addEventListener("input", () => this.computeNet());
      document.getElementById("wd-fee").addEventListener("input", () => this.computeNet());

      // Inputs OTP : focus auto suivant
      document.querySelectorAll(".otp-input").forEach((input, idx) => {
        input.addEventListener("input", (e) => {
          const val = e.target.value;
          if (val && idx < 4) {
            document.querySelectorAll(".otp-input")[idx + 1].focus();
          }
        });
        input.addEventListener("keydown", (e) => {
          if (e.key === "Backspace" && !e.target.value && idx > 0) {
            document.querySelectorAll(".otp-input")[idx - 1].focus();
          }
        });
      });
    }
  },

  computeNet() {
    const gross = parseFloat(document.getElementById("wd-amount-gross").value) || 0;
    const fee = parseFloat(document.getElementById("wd-fee").value) || 0;
    const net = gross - fee;
    const netInput = document.getElementById("wd-amount-net");
    if (net >= 0) {
      netInput.value = net.toFixed(2);
    }
  },

  /* -----------------------------------------------------------
     Session & Auth
     ----------------------------------------------------------- */
  restoreSession() {
    const stored = localStorage.getItem("eecc_admin_session");
    if (stored) {
      try {
        const session = JSON.parse(stored);
        if (session.access_token && session.expires_at > Date.now()) {
          currentAccessToken = session.access_token;
          this.isAuthenticated = true;
          this.redirectIfAuthenticated();
          return;
        }
      } catch (_) {}
    }
    localStorage.removeItem("eecc_admin_session");
  },

  saveSession(accessToken, expiresInSeconds) {
    currentAccessToken = accessToken;
    this.isAuthenticated = true;
    localStorage.setItem(
      "eecc_admin_session",
      JSON.stringify({
        access_token: accessToken,
        expires_at: Date.now() + expiresInSeconds * 1000,
      })
    );
  },

  clearSession() {
    currentAccessToken = null;
    this.isAuthenticated = false;
    localStorage.removeItem("eecc_admin_session");
  },

  authHeaders() {
    return {
      ...API_HEADERS,
      Authorization: `Bearer ${currentAccessToken || SUPABASE_ANON_KEY}`,
    };
  },

  redirectIfAuthenticated() {
    const isLoginPage = window.location.pathname.endsWith("index.html") || window.location.pathname.endsWith("/admin/");
    if (this.isAuthenticated && isLoginPage) {
      window.location.href = "dashboard.html";
    }
  },

  /* -----------------------------------------------------------
     Login
     ----------------------------------------------------------- */
  async handleLogin(e) {
    e.preventDefault();
    const btn = document.getElementById("login-btn");
    const errorEl = document.getElementById("login-error");
    const email = document.getElementById("email").value.trim();
    const password = document.getElementById("password").value;

    btn.disabled = true;
    btn.textContent = "Connexion…";
    errorEl.style.display = "none";

    try {
      const res = await fetch(`${SUPABASE_URL}/auth/v1/token?grant_type=password`, {
        method: "POST",
        headers: {
          apikey: SUPABASE_ANON_KEY,
          Authorization: `Bearer ${SUPABASE_ANON_KEY}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ email, password }),
      });

      const data = await res.json();

      if (!res.ok) {
        throw new Error(data.error_description || data.msg || "Identifiants invalides");
      }

      this.saveSession(data.access_token, data.expires_in);
      window.location.href = "dashboard.html";
    } catch (err) {
      errorEl.textContent = err.message || "Erreur de connexion";
      errorEl.style.display = "flex";
      btn.disabled = false;
      btn.textContent = "Se connecter";
    }
  },

  /* -----------------------------------------------------------
     Navigation entre sections
     ----------------------------------------------------------- */
  bindNavigation() {
    document.querySelectorAll(".nav-item").forEach((item) => {
      item.addEventListener("click", (e) => {
        e.preventDefault();
        const section = item.getAttribute("data-section");
        this.showSection(section);
      });
    });
  },

  showSection(sectionId) {
    document.querySelectorAll(".content-section").forEach((s) => {
      s.classList.remove("active");
    });
    document.querySelectorAll(".nav-item").forEach((n) => {
      n.classList.remove("active");
    });

    const target = document.getElementById(`section-${sectionId}`);
    const targetNav = document.querySelector(`[data-section="${sectionId}"]`);

    if (target) target.classList.add("active");
    if (targetNav) targetNav.classList.add("active");

    const titles = {
      dashboard: "Tableau de Bord",
      tresorerie: "Trésorerie & Retraits",
      medias: "Médias & Podcasts",
      cultes: "Cultes & Replays",
      utilisateurs: "Utilisateurs & Rôles",
      parametres: "Paramètres",
    };
    const titleEl = document.getElementById("page-title");
    const subEl = document.getElementById("page-subtitle");
    if (titleEl) titleEl.textContent = titles[sectionId] || sectionId;
    if (subEl) subEl.textContent = "";

    if (sectionId === "dashboard") {
      this.loadDashboardData();
    } else if (sectionId === "tresorerie") {
      this.loadTresorerieData();
    }
  },

  bindLogout() {
    const btn = document.getElementById("logout-btn");
    if (btn) {
      btn.addEventListener("click", (e) => {
        e.preventDefault();
        this.clearSession();
        window.location.href = "index.html";
      });
    }
  },

  /* -----------------------------------------------------------
     Données du dashboard (dons depuis Supabase)
     ----------------------------------------------------------- */
  async loadDashboardData() {
    const tbody = document.getElementById("donations-tbody");
    const statDonations = document.getElementById("stat-donations");
    const statSub = document.getElementById("stat-donations-sub");

    if (!tbody) return;

    try {
      tbody.innerHTML = `<tr><td colspan="6" class="empty-state">Chargement des dons…</td></tr>`;

      const res = await fetch(
        `${SUPABASE_URL}/rest/v1/donations?select=id,reference,donor_name,donor_email,amount,currency,method,status,created_at&order=created_at.desc&limit=10`,
        { headers: this.authHeaders() }
      );

      if (!res.ok) throw new Error("Impossible de charger les dons");

      const donations = await res.json();

      if (!donations || donations.length === 0) {
        tbody.innerHTML = `<tr><td colspan="6" class="empty-state">Aucun don enregistré pour le moment.</td></tr>`;
        if (statDonations) {
          statDonations.textContent = "0 $";
          statSub.textContent = "0 transactions";
        }
        return;
      }

      let totalAmount = 0;
      tbody.innerHTML = donations
        .map((tx) => {
          const amount = Number(tx.amount) || 0;
          totalAmount += amount;
          const statusLabel =
            tx.status === "confirmed"
              ? "Confirmé"
              : tx.status === "failed"
                ? "Échoué"
                : tx.status.charAt(0).toUpperCase() + tx.status.slice(1);
          const date = new Date(tx.created_at).toLocaleDateString("fr-FR", {
            day: "numeric",
            month: "short",
            hour: "2-digit",
            minute: "2-digit",
          });
          const methodLabel = tx.method.charAt(0).toUpperCase() + tx.method.slice(1);
          const donor = tx.donor_name || tx.donor_email || "Donateur anonyme";
          const badgeClass = tx.status === "confirmed" ? "status-ok" : "";

          return `
            <tr>
              <td class="font-mono font-medium text-slate-600">${tx.reference}</td>
              <td class="font-semibold text-slate-900">${donor}</td>
              <td class="font-bold text-success">${amount.toLocaleString()} ${tx.currency}</td>
              <td class="text-slate-600">${methodLabel}</td>
              <td><span class="status-badge ${badgeClass}">${statusLabel}</span></td>
              <td class="text-slate-500">${date}</td>
            </tr>
          `;
        })
        .join("");

      if (statDonations) {
        statDonations.textContent = `${totalAmount.toLocaleString()} $`;
        statSub.textContent = `${donations.length} transactions`;
      }
    } catch (err) {
      tbody.innerHTML = `<tr><td colspan="6" class="empty-state" style="color: var(--danger);">Erreur : ${err.message}</td></tr>`;
    }
  },

  /* -----------------------------------------------------------
     Trésorerie & Retraits
     ----------------------------------------------------------- */
  async loadTresorerieData() {
    const tbody = document.getElementById("withdrawals-tbody");
    const alertEl = document.getElementById("tresorerie-alert");
    const soldeEl = document.getElementById("tresorerie-solde");
    const donsEl = document.getElementById("tresorerie-dons");
    const enAttenteEl = document.getElementById("tresorerie-en-attente");

    if (!tbody) return;

    alertEl.style.display = "none";
    tbody.innerHTML = `<tr><td colspan="7" class="empty-state">Chargement des retraits…</td></tr>`;

    try {
      // Demandes de retrait
      const res = await fetch(
        `${SUPABASE_URL}/rest/v1/withdrawal_requests?select=id,reference,requested_by,amount_gross,fee,amount_net,currency,destination_account,destination_provider,status,created_at&order=created_at.desc`,
        { headers: this.authHeaders() }
      );

      if (!res.ok) throw new Error("Impossible de charger les retraits");

      const withdrawals = await res.json();

      // Dons confirmés (30 jours) pour le résumé
      const trenteJours = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString();
      const donsRes = await fetch(
        `${SUPABASE_URL}/rest/v1/donations?select=amount&status=eq.confirmed&created_at=gte.${trenteJours}`,
        { headers: this.authHeaders() }
      );
      const dons = (await donsRes.json()) || [];
      const totalDons = dons.reduce((sum, d) => sum + (Number(d.amount) || 0), 0);

      // Résumé
      if (soldeEl) soldeEl.textContent = `${totalDons.toLocaleString()} $`;
      if (donsEl) donsEl.textContent = `${totalDons.toLocaleString()} $`;
      if (enAttenteEl) {
        const pending = withdrawals.filter((w) => w.status === "pending_otp" || w.status === "processing").length;
        enAttenteEl.textContent = pending;
      }

      // Table
      if (!withdrawals || withdrawals.length === 0) {
        tbody.innerHTML = `<tr><td colspan="7" class="empty-state">Aucune demande de retrait pour le moment.</td></tr>`;
        return;
      }

      const statusLabel = (s) => {
        switch (s) {
          case "draft": return "Brouillon";
          case "pending_otp": return "En attente OTP";
          case "otp_verified": return "OTP vérifié";
          case "processing": return "En traitement";
          case "completed": return "Terminé";
          case "failed": return "Échoué";
          case "cancelled": return "Annulé";
          case "locked": return "Verrouillé";
          case "otp_expired": return "OTP expiré";
          default: return s;
        }
      };

      const badgeClass = (s) => {
        switch (s) {
          case "completed": return "status-ok";
          case "pending_otp":
          case "otp_verified":
          case "processing": return "";
          case "failed":
          case "locked":
          case "otp_expired": return "status-badge";
          default: return "";
        }
      };

      tbody.innerHTML = withdrawals
        .map((w) => {
          const date = new Date(w.created_at).toLocaleDateString("fr-FR", {
            day: "numeric",
            month: "short",
            hour: "2-digit",
            minute: "2-digit",
          });
          const destinationMasked = w.destination_account
            ? w.destination_account.replace(/\d(?=\d{4})/g, "*")
            : "—";
          const actionDisabled = w.status !== "pending_otp" ? "disabled" : "";
          const actionLabel = w.status === "pending_otp" ? "Confirmer (OTP)" : "Voir";

          return `
            <tr>
              <td class="font-mono font-medium text-slate-600">${w.reference}</td>
              <td class="font-semibold text-slate-900">${w.amount_net.toLocaleString()} ${w.currency}</td>
              <td class="text-slate-600">${destinationMasked}</td>
              <td class="text-slate-600">${w.destination_provider}</td>
              <td><span class="status-badge ${badgeClass(w.status)}">${statusLabel(w.status)}</span></td>
              <td class="text-slate-500">${date}</td>
              <td>
                <button class="btn btn-outline" onclick="app.openOtpModal('${w.id}')" ${actionDisabled}>
                  ${actionLabel}
                </button>
              </td>
            </tr>
          `;
        })
        .join("");
    } catch (err) {
      tbody.innerHTML = `<tr><td colspan="7" class="empty-state" style="color: var(--danger);">Erreur : ${err.message}</td></tr>`;
      if (alertEl) {
        alertEl.textContent = "Erreur lors du chargement des données de trésorerie.";
        alertEl.style.display = "flex";
      }
    }
  },

  /* -----------------------------------------------------------
     Formulaire de création de demande de retrait
     ----------------------------------------------------------- */
  openWithdrawalForm() {
    document.getElementById("withdrawal-form").reset();
    document.getElementById("wd-fee").value = "0";
    document.getElementById("wd-amount-net").value = "0";
    document.getElementById("withdrawal-form-error").style.display = "none";
    document.getElementById("withdrawal-modal").style.display = "block";
  },

  closeWithdrawalForm() {
    document.getElementById("withdrawal-modal").style.display = "none";
  },

  async handleCreateWithdrawal(e) {
    e.preventDefault();
    const btn = document.getElementById("wd-submit-btn");
    const errorEl = document.getElementById("withdrawal-form-error");

    const amount_gross = parseFloat(document.getElementById("wd-amount-gross").value);
    const fee = parseFloat(document.getElementById("wd-fee").value) || 0;
    const amount_net = parseFloat(document.getElementById("wd-amount-net").value);
    const currency = document.getElementById("wd-currency").value;
    const destination_account = document.getElementById("wd-destination-account").value.trim();
    const destination_provider = document.getElementById("wd-destination-provider").value;

    errorEl.style.display = "none";
    btn.disabled = true;
    btn.textContent = "Création…";

    try {
      const res = await fetch(`${SUPABASE_URL}/functions/v1/create-withdrawal-request`, {
        method: "POST",
        headers: this.authHeaders(),
        body: JSON.stringify({
          amount_gross,
          fee,
          amount_net,
          currency,
          destination_account,
          destination_provider,
        }),
      });

      const data = await res.json();

      if (!res.ok) {
        throw new Error(data.error || "Erreur lors de la création de la demande");
      }

      this.closeWithdrawalForm();
      this.loadTresorerieData();

      // Ouvrir directement la vérification OTP
      currentWithdrawalId = data.withdrawal.id;
      this.openOtpModal(data.withdrawal.id, data.withdrawal.reference);
    } catch (err) {
      errorEl.textContent = err.message || "Erreur inconnue";
      errorEl.style.display = "flex";
      btn.disabled = false;
      btn.textContent = "Créer la demande";
    }
  },

  /* -----------------------------------------------------------
     OTP — Envoi et vérification
     ----------------------------------------------------------- */
  openOtpModal(withdrawalId, reference) {
    currentWithdrawalId = withdrawalId;
    document.getElementById("otp-error").style.display = "none";
    document.getElementById("otp-attempts").textContent = "";
    document.querySelectorAll(".otp-input").forEach((i) => (i.value = ""));
    document.querySelector(".otp-input").focus();
    otpTimeLeft = 5 * 60; // 5 minutes
    this.updateOtpTimer();
    this.startOtpTimer();
    document.getElementById("otp-modal").style.display = "block";
  },

  closeOtpModal() {
    this.stopOtpTimer();
    document.getElementById("otp-modal").style.display = "none";
    currentWithdrawalId = null;
  },

  startOtpTimer() {
    this.stopOtpTimer();
    otpTimerInterval = setInterval(() => {
      otpTimeLeft--;
      this.updateOtpTimer();
      if (otpTimeLeft <= 0) {
        this.stopOtpTimer();
        document.getElementById("otp-timer").textContent =
          "Ce code a expiré. Demandez un nouveau code.";
      }
    }, 1000);
  },

  stopOtpTimer() {
    if (otpTimerInterval) {
      clearInterval(otpTimerInterval);
      otpTimerInterval = null;
    }
  },

  updateOtpTimer() {
    const m = Math.floor(otpTimeLeft / 60);
    const s = otpTimeLeft % 60;
    document.getElementById("otp-timer-value").textContent = `${m}:${s < 10 ? "0" : ""}${s}`;
  },

  async resendOtp() {
    if (!currentWithdrawalId) return;
    const btn = event ? event.target : null;
    const originalText = btn ? btn.textContent : "";
    if (btn) {
      btn.disabled = true;
      btn.textContent = "Envoi…";
    }

    try {
      const res = await fetch(`${SUPABASE_URL}/functions/v1/brevo-send-otp`, {
        method: "POST",
        headers: this.authHeaders(),
        body: JSON.stringify({ withdrawal_id: currentWithdrawalId }),
      });

      const data = await res.json();

      if (!res.ok) {
        throw new Error(data.error || "Échec de l'envoi du code");
      }

      otpTimeLeft = 5 * 60;
      this.updateOtpTimer();

      const masked = document.getElementById("otp-masked-email");
      if (data.masked_email) masked.textContent = data.masked_email;

      this.showOtpTemporaryMessage("Un nouveau code a été envoyé.", "success");
    } catch (err) {
      this.showOtpError(err.message || "Échec de l'envoi du code");
    } finally {
      if (btn) {
        btn.disabled = false;
        btn.textContent = originalText;
      }
    }
  },

  async submitOtp() {
    if (!currentWithdrawalId) return;
    const code = Array.from(document.querySelectorAll(".otp-input"))
      .map((i) => i.value)
      .join("");

    if (code.length !== 5) {
      this.showOtpError("Veuillez saisir les 5 caractères du code.");
      return;
    }

    const btn = event ? event.target : null;
    const originalText = btn ? btn.textContent : "";
    if (btn) {
      btn.disabled = true;
      btn.textContent = "Vérification…";
    }

    try {
      const res = await fetch(`${SUPABASE_URL}/functions/v1/withdrawal-verify-otp`, {
        method: "POST",
        headers: this.authHeaders(),
        body: JSON.stringify({
          withdrawal_id: currentWithdrawalId,
          otp_code: code,
        }),
      });

      const data = await res.json();

      if (!res.ok) {
        throw new Error(data.error || "Code invalide ou expiré");
      }

      this.closeOtpModal();
      this.loadTresorerieData();

      const msgDiv = document.createElement("div");
      msgDiv.className = "alert" + " " + (data.success ? "" : "alert-error");
      msgDiv.style.cssText = "margin-top:16px; padding:10px 14px; border-radius:8px; background:#ecfdf5; color:#166534; border:1px solid #bbf7d0; font-size:13px;";
      msgDiv.textContent = `Retrait ${data.status} (référence ${data.provider_reference || currentWithdrawalId}).`;
      const tresorerieSection = document.getElementById("section-tresorerie");
      const existing = tresorerieSection.querySelector(".result-msg");
      if (existing) existing.remove();
      msgDiv.classList.add("result-msg");
      tresorerieSection.insertBefore(msgDiv, tresorerieSection.firstChild);
    } catch (err) {
      this.showOtpError(err.message || "Échec de la vérification");
    } finally {
      if (btn) {
        btn.disabled = false;
        btn.textContent = originalText;
      }
    }
  },

  showOtpError(message) {
    const el = document.getElementById("otp-error");
    el.textContent = message;
    el.style.display = "flex";
  },

  showOtpTemporaryMessage(message, type) {
    // Afficher un message temporaire sous les inputs OTP
    const existing = document.getElementById("otp-temp-msg");
    if (existing) existing.remove();
    const div = document.createElement("div");
    div.id = "otp-temp-msg";
    div.style.cssText = "margin-top:10px; font-size:12px; padding:8px 12px; border-radius:6px; text-align:center; " +
      (type === "success" ? "background:#ecfdf5; color:#166534; border:1px solid #bbf7d0;" : "background:#fef2f2; color:#dc2626; border:1px solid #fecaca;");
    div.textContent = message;
    const inputsContainer = document.getElementById("otp-inputs");
    inputsContainer.parentNode.insertBefore(div, inputsContainer.nextSibling);
    setTimeout(() => div.remove(), 5000);
  },
};

/* -------------------------------------------------------------
   Démarrage
   ------------------------------------------------------------- */
document.addEventListener("DOMContentLoaded", () => {
  app.init();
});
