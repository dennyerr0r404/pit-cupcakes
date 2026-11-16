/* Painel do administrador: pedidos (RF08) e produtos (RF07) */
"use strict";
(async () => {
  if (!Sessao.exigir()) return;
  if (!Sessao.ehAdmin()) {
    $("main").innerHTML = `<div class="aviso erro" role="alert">Você não tem permissão para acessar esta área.</div>
      <a class="btn" href="index.html">Voltar à loja</a>`;
    return;
  }
  const [lojaBtn, , sairBtn] = $(".topo").children;
  lojaBtn.innerHTML = ICONES.inicio;
  sairBtn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M15 4h4v16h-4M10 8l-4 4 4 4M6 12h10"/></svg>';
  sairBtn.onclick = async () => { await Sessao.sair(); location.replace("index.html"); };

  const IMAGENS = [
    ["/img/velvet-red.svg", "Velvet Red"], ["/img/chocolate-belga.svg", "Chocolate"], ["/img/baunilha.svg", "Baunilha"],
    ["/img/cenoura-vegana.svg", "Cenoura"], ["/img/limao-vegano.svg", "Limão"], ["/img/pistache.svg", "Pistache"],
    ["/img/frutas-vermelhas.svg", "Frutas vermelhas"], ["/img/cupcake-padrao.svg", "Padrão"],
  ];
  const ROTULO_ACAO = {
    EM_PREPARO: "Iniciar preparo", SAIU_PARA_ENTREGA: "Saiu para entrega",
    PRONTO_PARA_RETIRADA: "Pronto p/ retirada", ENTREGUE: "Marcar entregue", CANCELADO: "Cancelar",
  };

  // ---------------- Abas ----------------
  $$(".admin-abas button").forEach(b => b.addEventListener("click", () => {
    $$(".admin-abas button").forEach(x => { x.classList.toggle("ativo", x === b); x.setAttribute("aria-selected", x === b); });
    $("#abaPedidos").hidden = b.dataset.aba !== "pedidos";
    $("#abaProdutos").hidden = b.dataset.aba !== "produtos";
    $("#abaFeedbacks").hidden = b.dataset.aba !== "feedbacks";
    if (b.dataset.aba === "produtos") carregarProdutos();
    else if (b.dataset.aba === "feedbacks") carregarFeedbacks();
    else carregarPedidos();
  }));

  // ---------------- Pedidos ----------------
  let situacao = "abertos";
  $("#filtros").addEventListener("click", e => {
    const b = e.target.closest("button"); if (!b) return;
    situacao = b.dataset.sit;
    $$("#filtros button").forEach(x => x.classList.toggle("ativo", x === b));
    carregarPedidos();
  });
  $("#recarregar").onclick = () => carregarPedidos();

  async function carregarPedidos() {
    const el = $("#listaPedidos");
    if (!el.children.length) el.innerHTML = '<div class="carregando">Carregando pedidos…</div>';
    try {
      const pedidos = await api("admin/pedidos" + (situacao ? `?situacao=${situacao}` : ""));
      if (!pedidos.length) { el.innerHTML = telaVazia(situacao === "abertos" ? "Nenhum pedido em aberto." : "Nenhum pedido encontrado."); return; }
      el.innerHTML = `<div class="grade-admin">${pedidos.map(p => `
        <article class="ped-admin">
          <div class="h"><span>#${p.id} · ${esc(p.cliente)}</span><span>${moeda(p.valorTotal)}</span></div>
          <div class="m">${dataHora(p.dataPedido)}<br>
            ${NOMES_TIPO[p.tipoEntrega]} · ${NOMES_PAGTO[p.formaPagamento]}${p.clienteTelefone ? ` · ${esc(p.clienteTelefone)}` : ""}
            ${p.enderecoEntrega ? `<br><b>Endereço:</b> ${esc(p.enderecoEntrega)}` : ""}
            ${p.observacao ? `<br><b>Obs.:</b> ${esc(p.observacao)}` : ""}</div>
          <ul>${p.itens.map(i => `<li><span>${i.quantidade}× ${esc(i.produto)}</span><span>${moeda(i.subtotal)}</span></li>`).join("")}</ul>
          <div class="acoes-status">
            <span class="pill st-${p.status}">${esc(p.statusRotulo)}</span>
            ${p.proximosStatus.map(s => `<button type="button" class="btn ${s === "CANCELADO" ? "perigo" : "sec"} peq"
                data-pedido="${p.id}" data-status="${s}">${ROTULO_ACAO[s] || s}</button>`).join("")}
          </div>
        </article>`).join("")}</div>`;
    } catch (e) {
      if (e.status === 401) { Sessao.irParaLogin(); return; }
      el.innerHTML = telaErro(e);
    }
  }

  $("#listaPedidos").addEventListener("click", async e => {
    const b = e.target.closest("[data-status]"); if (!b) return;
    const { pedido, status } = b.dataset;
    if (status === "CANCELADO" && !confirm(`Cancelar o pedido #${pedido}? Os itens voltam ao estoque.`)) return;
    b.disabled = true;
    try {
      const p = await api(`admin/pedidos/${pedido}/status`, { metodo: "PATCH", corpo: { status } });
      toast(`Pedido #${p.id}: ${p.statusRotulo}`);
      carregarPedidos();
    } catch (err) { toast(err.message, true); b.disabled = false; }
  });

  // Atualiza a lista de pedidos a cada 30 segundos enquanto a aba está aberta.
  setInterval(() => { if (!$("#abaPedidos").hidden && !document.hidden) carregarPedidos(); }, 30000);

  // ---------------- Produtos ----------------
  let produtos = [], categorias = [], editando = null;

  async function carregarProdutos() {
    const el = $("#listaProdutos");
    el.innerHTML = '<div class="carregando">Carregando produtos…</div>';
    try {
      [produtos, categorias] = await Promise.all([api("admin/produtos"), api("admin/categorias")]);
      $("#contagem").textContent = `${produtos.length} produto(s) · ${produtos.filter(p => p.ativo).length} ativo(s)`;
      el.innerHTML = `<table class="tabela">
        <thead><tr><th></th><th>Produto</th><th class="col-opc">Categoria</th><th class="num">Preço</th><th class="num">Estoque</th><th>Situação</th><th></th></tr></thead>
        <tbody>${produtos.map(p => `
          <tr class="${p.ativo ? "" : "inativo"}">
            <td><div class="mini">${imagemProduto(p)}</div></td>
            <td><b>${esc(p.nome)}</b></td>
            <td class="col-opc">${esc(p.categoria)}</td>
            <td class="num">${moeda(p.preco)}</td>
            <td class="num">${p.quantidadeEstoque === 0 ? '<span class="tag esgotado">0</span>' : p.quantidadeEstoque}</td>
            <td>${p.ativo ? '<span class="tag">Ativo</span>' : '<span class="tag esgotado">Inativo</span>'}</td>
            <td style="white-space:nowrap">
              <button class="btn sec peq" type="button" data-editar="${p.id}">Editar</button>
              <button class="btn ${p.ativo ? "perigo" : "sec"} peq" type="button" data-ativo="${p.id}" data-valor="${!p.ativo}">${p.ativo ? "Desativar" : "Ativar"}</button>
            </td>
          </tr>`).join("")}</tbody></table>`;
    } catch (e) {
      if (e.status === 401) { Sessao.irParaLogin(); return; }
      el.innerHTML = telaErro(e);
    }
  }

  $("#listaProdutos").addEventListener("click", async e => {
    const ed = e.target.closest("[data-editar]");
    if (ed) { abrirForm(produtos.find(p => p.id === Number(ed.dataset.editar))); return; }
    const at = e.target.closest("[data-ativo]");
    if (at) {
      at.disabled = true;
      try {
        const p = await api(`admin/produtos/${at.dataset.ativo}/ativo`, { metodo: "PATCH", corpo: { ativo: at.dataset.valor === "true" } });
        toast(`${p.nome} ${p.ativo ? "ativado" : "desativado"}`);
        carregarProdutos();
      } catch (err) { toast(err.message, true); at.disabled = false; }
    }
  });

  // ---------------- Formulário de produto ----------------
  const dlg = $("#dlg"), f = $("#formProduto");
  const TIPOS_FOTO = ["image/jpeg", "image/png", "image/webp"], MAX_FOTO = 2 * 1024 * 1024;
  let arquivoFoto = null, urlPreviewArquivo = null;

  $("#pImagem").addEventListener("change", () => {
    // Escolheu outra imagem na lista: descarta a foto do computador que ainda não foi enviada.
    if ($("#pImagem").value !== "__novo" && arquivoFoto) {
      arquivoFoto = null; $("#pArquivo").value = "";
      $("#pImagem").querySelector('option[value="__novo"]')?.remove();
      $("#pArquivoInfo").textContent = "JPG, PNG ou WebP, até 2 MB. Substitui a imagem escolhida acima.";
    }
    $("#pImagemUrl").hidden = $("#pImagem").value !== "__url";
    atualizarPreview();
  });
  $("#pImagemUrl").addEventListener("input", atualizarPreview);
  $("#novo").onclick = () => abrirForm(null);
  $("#cancelar").onclick = () => dlg.close();

  // Laudo, item 7: foto enviada do computador, com conferência de tipo e tamanho antes do envio.
  $("#pArquivo").addEventListener("change", () => {
    const arq = $("#pArquivo").files[0];
    limparErros(f);
    if (!arq) return;
    if (!TIPOS_FOTO.includes(arq.type)) { marcarCampo(f, "arquivo", "Formato não aceito. Envie uma foto JPG, PNG ou WebP."); $("#pArquivo").value = ""; return; }
    if (arq.size > MAX_FOTO) { marcarCampo(f, "arquivo", "A foto deve ter no máximo 2 MB."); $("#pArquivo").value = ""; return; }
    arquivoFoto = arq;
    $("#pArquivoInfo").textContent = `Foto selecionada: ${arq.name} (${(arq.size / 1024).toFixed(0)} KB). Será enviada ao salvar.`;
    const sel = $("#pImagem");
    if (!sel.querySelector('option[value="__novo"]')) sel.insertAdjacentHTML("afterbegin", '<option value="__novo">Foto do computador (nova)</option>');
    sel.value = "__novo"; $("#pImagemUrl").hidden = true;
    atualizarPreview();
  });

  function urlImagemEscolhida() {
    const v = f.imagem.value;
    if (v === "__url") return f.imagemUrl.value.trim() || null;
    if (v === "__foto") return editando?.imagemUrl ?? null;
    if (v === "__novo") return editando?.imagemUrl ?? null;   // a foto nova é enviada logo após salvar
    return v;
  }

  function atualizarPreview() {
    if (urlPreviewArquivo) { URL.revokeObjectURL(urlPreviewArquivo); urlPreviewArquivo = null; }
    if (arquivoFoto) urlPreviewArquivo = URL.createObjectURL(arquivoFoto);
    const img = $("#pPreview");
    img.onerror = () => { img.onerror = null; img.src = "img/cupcake-padrao.svg"; };
    img.src = urlPreviewArquivo || urlImagemEscolhida() || "img/cupcake-padrao.svg";
  }

  function abrirForm(p) {
    editando = p;
    arquivoFoto = null; $("#pArquivo").value = "";
    $("#pArquivoInfo").textContent = "JPG, PNG ou WebP, até 2 MB. Substitui a imagem escolhida acima.";
    limparErros(f); $("#dlgAlerta").innerHTML = "";
    $("#dlgTitulo").textContent = p ? `Editar: ${p.nome}` : "Novo produto";
    $("#pCategoria").innerHTML = categorias.map(c => `<option value="${c.id}">${esc(c.nome)}</option>`).join("");
    const temFoto = !!p?.imagemUrl?.startsWith(`/api/produtos/${p.id}/imagem`);
    $("#pImagem").innerHTML = (temFoto ? '<option value="__foto">Foto enviada (atual)</option>' : "") +
      IMAGENS.map(([v, n]) => `<option value="${v}">Ilustração: ${n}</option>`).join("") +
      '<option value="__url">Outra (informar URL)</option>';
    f.nome.value = p?.nome ?? "";
    f.categoria.value = p?.categoriaId ?? categorias[0]?.id ?? "";
    f.preco.value = p ? p.preco.toFixed(2) : "";
    f.estoque.value = p?.quantidadeEstoque ?? 0;
    f.descricao.value = p?.descricao ?? "";
    f.ativo.checked = p ? p.ativo : true;
    const img = p?.imagemUrl ?? IMAGENS[IMAGENS.length - 1][0];
    if (temFoto) { f.imagem.value = "__foto"; f.imagemUrl.value = ""; }
    else if (IMAGENS.some(([v]) => v === img)) { f.imagem.value = img; f.imagemUrl.value = ""; }
    else { f.imagem.value = "__url"; f.imagemUrl.value = img; }
    $("#pImagemUrl").hidden = f.imagem.value !== "__url";
    atualizarPreview();
    dlg.showModal();
    f.nome.focus();
  }

  f.addEventListener("submit", async e => {
    e.preventDefault();
    limparErros(f); $("#dlgAlerta").innerHTML = "";
    const corpo = {
      nome: f.nome.value.trim(),
      categoriaId: Number(f.categoria.value),
      preco: Number(String(f.preco.value).replace(",", ".")),
      quantidadeEstoque: Number(f.estoque.value),
      imagemUrl: urlImagemEscolhida(),
      descricao: f.descricao.value.trim() || null,
      ativo: f.ativo.checked,
    };
    let ok = true;
    if (corpo.nome.length < 2) { marcarCampo(f, "nome", "Informe o nome do produto."); ok = false; }
    // Laudo, item 6: aviso imediato de nome repetido (o servidor também confere).
    else if (produtos.some(p => p.id !== editando?.id && p.nome.trim().toLowerCase() === corpo.nome.toLowerCase())) {
      marcarCampo(f, "nome", "Já existe um produto com este nome."); ok = false;
    }
    if (f.preco.value === "" || !(corpo.preco >= 0)) { marcarCampo(f, "preco", "Informe um preço válido (não negativo)."); ok = false; }
    if (f.estoque.value === "" || !Number.isInteger(corpo.quantidadeEstoque) || corpo.quantidadeEstoque < 0) { marcarCampo(f, "quantidadeEstoque", "Informe um estoque válido (inteiro, não negativo)."); ok = false; }
    if (!ok) return;
    const b = $("#salvar"); b.disabled = true; b.textContent = arquivoFoto ? "Salvando e enviando foto…" : "Salvando…";
    try {
      let salvo = editando
        ? await api(`admin/produtos/${editando.id}`, { metodo: "PUT", corpo })
        : await api("admin/produtos", { metodo: "POST", corpo });
      editando = salvo;   // se a foto falhar, um novo "Salvar" altera em vez de criar outro produto
      if (arquivoFoto) {
        const dados = new FormData();
        dados.append("arquivo", arquivoFoto);
        salvo = await api(`admin/produtos/${salvo.id}/imagem`, { metodo: "POST", corpo: dados });
        arquivoFoto = null;
      }
      dlg.close();
      toast(`${salvo.nome} salvo`);
      carregarProdutos();
    } catch (err) {
      if (!mostrarErros(f, err)) $("#dlgAlerta").innerHTML = `<div class="aviso erro" role="alert">${esc(err.message)}</div>`;
      if (editando) $("#dlgTitulo").textContent = `Editar: ${editando.nome}`;
    } finally { b.disabled = false; b.textContent = "Salvar"; }
  });

  // ---------------- Feedbacks (RF12) ----------------
  let sitFeedback = "nao_lidos";
  const media = v => v == null ? "—" : `${Number(v).toFixed(1).replace(".", ",")} <small style="font-size:14px;color:#F2B01E">★</small>`;

  $("#filtrosFeedback").addEventListener("click", e => {
    const b = e.target.closest("button"); if (!b) return;
    sitFeedback = b.dataset.sit;
    $$("#filtrosFeedback button").forEach(x => x.classList.toggle("ativo", x === b));
    carregarFeedbacks();
  });

  async function carregarFeedbacks() {
    const el = $("#listaFeedbacks");
    try {
      const { resumo, itens } = await api("admin/feedbacks" + (sitFeedback ? `?situacao=${sitFeedback}` : ""));
      $("#contFeedback").textContent = resumo.naoLidos || "";
      $("#resumoFeedback").innerHTML = `
        <div><span>Feedbacks recebidos</span><b>${resumo.total}</b></div>
        <div><span>Não lidos</span><b>${resumo.naoLidos}</b></div>
        <div><span>Média dos produtos</span><b>${media(resumo.mediaProduto)}</b></div>
        <div><span>Média do atendimento</span><b>${media(resumo.mediaAtendimento)}</b></div>`;
      el.innerHTML = itens.length ? itens.map(fb => cartaoFeedback(fb, true)).join("")
        : `<div style="grid-column:1/-1">${telaVazia(sitFeedback ? "Nenhum feedback novo." : "Nenhum feedback recebido ainda.")}</div>`;
    } catch (e) {
      if (e.status === 401) { Sessao.irParaLogin(); return; }
      el.innerHTML = telaErro(e);
    }
  }

  $("#listaFeedbacks").addEventListener("click", async e => {
    const b = e.target.closest("[data-lido]"); if (!b) return;
    b.disabled = true;
    try {
      await api(`admin/feedbacks/${b.dataset.lido}/lido`, { metodo: "PATCH", corpo: { lido: b.dataset.valor === "true" } });
      carregarFeedbacks();
    } catch (err) { toast(err.message, true); b.disabled = false; }
  });

  // Mostra quantos feedbacks novos existem logo ao abrir o painel.
  api("admin/feedbacks?situacao=nao_lidos").then(r => { $("#contFeedback").textContent = r.resumo.naoLidos || ""; }).catch(() => {});

  carregarPedidos();
})();
