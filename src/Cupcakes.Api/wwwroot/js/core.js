/* =========================================================
   Cupcakes Gourmet - funções comuns a todas as páginas
   (API, sessão, carrinho, formatação e layout)
   ========================================================= */
"use strict";

// ---------- Armazenamento local (protegido contra navegação privada) ----------
const Armazem = {
  ler(chave, padrao) {
    try { const v = localStorage.getItem(chave); return v ? JSON.parse(v) : padrao; } catch { return padrao; }
  },
  gravar(chave, valor) { try { localStorage.setItem(chave, JSON.stringify(valor)); } catch { /* ignora */ } },
  remover(chave) { try { localStorage.removeItem(chave); } catch { /* ignora */ } },
};

// ---------- Sessão ----------
// O login fica num cookie HttpOnly enviado automaticamente pelo navegador.
// Aqui guardamos só os dados do usuário para montar a interface.
const Sessao = {
  CHAVE: "cupcakes.usuario",
  atual() { const s = Armazem.ler(this.CHAVE, null); return s && s.usuario ? s : null; },
  usuario() { return this.atual()?.usuario ?? null; },
  ehAdmin() { return this.usuario()?.perfil === "ADMIN"; },
  salvar(resp) { Armazem.gravar(this.CHAVE, { usuario: resp.usuario }); },
  limpar() { Armazem.remover(this.CHAVE); },
  async sair() {
    try { await fetch("/api/auth/logout", { method: "POST", headers: { "Content-Type": "application/json" } }); } catch { /* ignora */ }
    this.limpar();
  },
  irParaLogin() {
    location.href = "login.html?voltar=" + encodeURIComponent(location.pathname.replace(/^\//, "") + location.search);
  },
  /** Redireciona ao login se não houver sessão; volta para a página atual depois. */
  exigir() {
    if (this.atual()) return true;
    this.irParaLogin();
    return false;
  },
};

// ---------- Carrinho (só ids e quantidades; preços sempre vêm da API) ----------
const Carrinho = {
  CHAVE: "cupcakes.carrinho",
  itens() { return Armazem.ler(this.CHAVE, []).filter(i => i && i.id > 0 && i.qtd > 0); },
  salvar(itens) { Armazem.gravar(this.CHAVE, itens); atualizarBadge(); },
  quantidadeTotal() { return this.itens().reduce((s, i) => s + i.qtd, 0); },
  qtdDe(id) { return this.itens().find(i => i.id === id)?.qtd ?? 0; },
  adicionar(id, qtd = 1) {
    const itens = this.itens();
    const it = itens.find(i => i.id === id);
    if (it) it.qtd += qtd; else itens.push({ id, qtd });
    this.salvar(itens);
  },
  definir(id, qtd) {
    let itens = this.itens();
    if (qtd <= 0) itens = itens.filter(i => i.id !== id);
    else { const it = itens.find(i => i.id === id); if (it) it.qtd = qtd; else itens.push({ id, qtd }); }
    this.salvar(itens);
  },
  remover(id) { this.salvar(this.itens().filter(i => i.id !== id)); },
  limpar() { this.salvar([]); },
};

// ---------- API ----------
class ErroApi extends Error {
  constructor(status, mensagem, erros) { super(mensagem); this.status = status; this.erros = erros || {}; }
}

async function api(caminho, { metodo = "GET", corpo } = {}) {
  const headers = { "Accept": "application/json" };
  if (corpo !== undefined) headers["Content-Type"] = "application/json";

  let resp;
  try {
    resp = await fetch("/api/" + caminho, { method: metodo, headers, body: corpo !== undefined ? JSON.stringify(corpo) : undefined });
  } catch {
    throw new ErroApi(0, "Sem conexão com o servidor. Verifique sua internet e tente novamente.");
  }
  let dados = null;
  const tipo = resp.headers.get("content-type") || "";
  if (tipo.includes("json")) dados = await resp.json().catch(() => null);

  if (!resp.ok) {
    if (resp.status === 401) Sessao.limpar(); // sessão expirada no servidor
    throw new ErroApi(resp.status, dados?.mensagem || "Não foi possível concluir a operação.", dados?.erros);
  }
  return dados;
}

// ---------- Formatação ----------
const moeda = v => Number(v).toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
const dataHora = iso => new Date(iso).toLocaleString("pt-BR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
const hora = iso => new Date(iso).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
const NOMES_PAGTO = { PIX: "PIX", DINHEIRO: "Dinheiro", CARTAO: "Cartão" };
const NOMES_TIPO = { ENTREGA: "Entrega", RETIRADA: "Retirada" };

/** Escapa texto antes de inserir em HTML. */
function esc(t) {
  return String(t ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}
const $ = (sel, raiz = document) => raiz.querySelector(sel);
const $$ = (sel, raiz = document) => [...raiz.querySelectorAll(sel)];
const params = new URLSearchParams(location.search);

function imagemProduto(p, alt) {
  const src = p.imagemUrl || "img/cupcake-padrao.svg";
  return `<img src="${esc(src)}" alt="${esc(alt ?? p.nome ?? p.produto ?? "")}" loading="lazy" onerror="this.onerror=null;this.src='img/cupcake-padrao.svg'">`;
}

// ---------- Ícones ----------
const ICONES = {
  voltar: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M19 12H5M11 6l-6 6 6 6"/></svg>',
  carrinho: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 4h2l2.4 11h10.4L20 8H7"/><circle cx="9" cy="19" r="1.6"/><circle cx="17" cy="19" r="1.6"/></svg>',
  inicio: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 3l9 8h-3v9h-5v-6h-2v6H6v-9H3z"/></svg>',
  pedidos: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01"/></svg>',
  perfil: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="8" r="4"/><path d="M4 21c1-4 4-6 8-6s7 2 8 6"/></svg>',
  cupcake: '<svg viewBox="0 0 64 64" fill="none" stroke="currentColor" stroke-width="3" stroke-linejoin="round"><path d="M14 34h36l-5 22H19z"/><path d="M12 34c-2-10 8-12 10-12 0-8 8-12 14-10 6 2 8 8 6 10 6 0 12 4 10 12"/></svg>',
};

// ---------- Layout ----------
/**
 * Monta o cabeçalho e a barra de navegação.
 * opcoes: { titulo, voltar (url|true), carrinho (bool), nav ('inicio'|'carrinho'|'pedidos'|'perfil'|false), banner }
 */
function montarLayout({ titulo = "", voltar = false, carrinho = false, nav = false } = {}) {
  const app = $(".app");
  if (titulo) {
    const topo = document.createElement("header");
    topo.className = "topo";
    const bVoltar = voltar
      ? `<button class="icone-btn" type="button" id="btnVoltar" aria-label="Voltar">${ICONES.voltar}</button>`
      : '<span class="espaco"></span>';
    const bCarrinho = carrinho
      ? `<a class="icone-btn" href="carrinho.html" aria-label="Carrinho">${ICONES.carrinho}<span class="badge" data-badge></span></a>`
      : '<span class="espaco"></span>';
    topo.innerHTML = `${bVoltar}<h1>${esc(titulo)}</h1>${bCarrinho}`;
    app.prepend(topo);
    if (voltar) $("#btnVoltar").addEventListener("click", () => {
      if (typeof voltar === "string") location.href = voltar;
      else if (history.length > 1 && document.referrer.startsWith(location.origin)) history.back();
      else location.href = "index.html";
    });
  }
  if (nav) {
    app.classList.add("com-nav");
    const n = document.createElement("nav");
    n.className = "nav";
    n.setAttribute("aria-label", "Navegação principal");
    const item = (id, href, ic, txt, extra = "") =>
      `<a href="${href}" class="${nav === id ? "ativo" : ""}" ${nav === id ? 'aria-current="page"' : ""}>${ICONES[ic]}${txt}${extra}</a>`;
    n.innerHTML =
      item("inicio", "index.html", "inicio", "Início") +
      item("carrinho", "carrinho.html", "carrinho", "Carrinho", '<span class="badge" data-badge></span>') +
      item("pedidos", "pedidos.html", "pedidos", "Pedidos") +
      item("perfil", "perfil.html", "perfil", "Perfil");
    app.append(n);
  }
  atualizarBadge();
}

function atualizarBadge() {
  const q = Carrinho.quantidadeTotal();
  $$("[data-badge]").forEach(b => { b.textContent = q > 0 ? (q > 99 ? "99+" : q) : ""; });
}

let _toastTimer;
function toast(msg, erro = false) {
  let t = $(".toast");
  if (!t) { t = document.createElement("div"); t.className = "toast"; t.setAttribute("role", "status"); document.body.append(t); }
  t.textContent = msg;
  t.classList.toggle("erro", erro);
  t.classList.add("visivel");
  clearTimeout(_toastTimer);
  _toastTimer = setTimeout(() => t.classList.remove("visivel"), 2600);
}

function telaVazia(msg, botao, href) {
  return `<div class="vazio">${ICONES.cupcake}<p>${esc(msg)}</p>${botao ? `<a class="btn" href="${href}">${esc(botao)}</a>` : ""}</div>`;
}
function telaErro(e) {
  return `<div class="aviso erro" role="alert">${esc(e.message || "Erro ao carregar.")}</div>`;
}

/** Mostra erros de validação da API nos campos do formulário (data-campo="nome"). */
function mostrarErros(form, erro) {
  $$(".campo", form).forEach(c => c.classList.remove("invalido"));
  let algum = false;
  for (const [campo, msgs] of Object.entries(erro.erros || {})) {
    const c = $(`[data-campo="${campo}"]`, form);
    if (c) { c.classList.add("invalido"); $(".erro", c).textContent = msgs[0]; algum = true; }
  }
  return algum;
}
function marcarCampo(form, campo, msg) {
  const c = $(`[data-campo="${campo}"]`, form);
  if (c) { c.classList.add("invalido"); $(".erro", c).textContent = msg; }
}
function limparErros(form) { $$(".campo", form).forEach(c => c.classList.remove("invalido")); }

// Ao digitar ou escolher de novo, some o destaque de erro daquele campo.
["input", "change"].forEach(ev => document.addEventListener(ev, e => {
  e.target.closest?.(".campo")?.classList.remove("invalido");
}));
