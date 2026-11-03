"""Reconstrói a parte de respostas do formulário 'PIT_atividade_1.docx'.

Mantém o cabeçalho, a introdução, nome/RGM e a escolha da situação-problema
do formulário original; substitui as caixas de texto flutuantes por
parágrafos com borda e imagens em linha (layout estável no Word e no
LibreOffice).

Uso: python montar_atividade1.py <pasta_docx_descompactado> <pasta_imagens>
"""
import os, re, shutil, sys
from xml.sax.saxutils import escape
from PIL import Image

U, IMG = sys.argv[1], sys.argv[2]
DOC = os.path.join(U, "word", "document.xml")
RELS = os.path.join(U, "word", "_rels", "document.xml.rels")

ARIAL = '<w:rFonts w:ascii="Arial" w:hAnsi="Arial" w:cs="Arial"/>'
_UNUSED_BOX = ('<w:pBdr>' + ''.join(f'<w:{s} w:val="single" w:sz="4" w:space="6" w:color="808080"/>'
       for s in ("top", "left", "bottom", "right")) + '</w:pBdr>')
CONTENT_EMU = 5_400_000  # largura útil da página (A4, margens 3 cm)


def run(text, bold=False, size=21):
    b = '<w:b/><w:bCs/>' if bold else ''
    return (f'<w:r><w:rPr>{ARIAL}{b}<w:sz w:val="{size}"/><w:szCs w:val="{size}"/></w:rPr>'
            f'<w:t xml:space="preserve">{escape(text)}</w:t></w:r>')


def rich(text, size=21):
    """Texto com trechos em negrito marcados por **...**."""
    parts = re.split(r'(\*\*.*?\*\*)', text)
    return ''.join(run(p[2:-2], True, size) if p.startswith('**') else run(p, False, size)
                   for p in parts if p)


def label(text, before=360):
    return (f'<w:p><w:pPr><w:keepNext/><w:spacing w:before="{before}" w:after="120" w:line="360" w:lineRule="auto"/>'
            f'<w:rPr>{ARIAL}<w:b/><w:bCs/></w:rPr></w:pPr>{run(text, True, 22)}</w:p>')


def box_par(text, num=None, after=60):
    numpr = f'<w:numPr><w:ilvl w:val="0"/><w:numId w:val="{num}"/></w:numPr>' if num else ''
    ind = '<w:ind w:left="454" w:hanging="284"/>' if num else ''
    return (f'<w:p><w:pPr>{numpr}<w:spacing w:before="0" w:after="{after}" w:line="300" w:lineRule="auto"/>'
            f'{ind}<w:jc w:val="both"/></w:pPr>{rich(text)}</w:p>')


def box(items):
    """Caixa de resposta: tabela de uma célula com borda.
    items: str (parágrafo), ('b', str) com marcador ou ('n', str) numerado."""
    pars = []
    for i, it in enumerate(items):
        last = i == len(items) - 1
        if isinstance(it, tuple):
            kind, t = it
            pars.append(box_par(t, num=3 if kind == 'b' else 2, after=40))
        else:
            pars.append(box_par(it, after=0 if last else 120))
    bd = ''.join(f'<w:{s} w:val="single" w:sz="4" w:space="0" w:color="808080"/>'
                 for s in ("top", "left", "bottom", "right"))
    return (f'<w:tbl><w:tblPr><w:tblW w:w="8504" w:type="dxa"/><w:tblBorders>{bd}</w:tblBorders>'
            f'<w:tblLayout w:type="fixed"/><w:tblCellMar><w:top w:w="100" w:type="dxa"/><w:left w:w="140" w:type="dxa"/>'
            f'<w:bottom w:w="100" w:type="dxa"/><w:right w:w="140" w:type="dxa"/></w:tblCellMar></w:tblPr>'
            f'<w:tblGrid><w:gridCol w:w="8504"/></w:tblGrid><w:tr><w:tc><w:tcPr><w:tcW w:w="8504" w:type="dxa"/></w:tcPr>'
            + ''.join(pars) + '</w:tc></w:tr></w:tbl>')


_img_id = [100]


def image(rid, path, width_emu, caption):
    w, h = Image.open(path).size
    cy = int(width_emu * h / w)
    _img_id[0] += 1
    n = _img_id[0]
    drawing = (
        f'<w:r><w:rPr><w:noProof/></w:rPr><w:drawing><wp:inline distT="0" distB="0" distL="0" distR="0">'
        f'<wp:extent cx="{width_emu}" cy="{cy}"/><wp:effectExtent l="0" t="0" r="0" b="0"/>'
        f'<wp:docPr id="{n}" name="Figura {n}" descr="{escape(caption)}"/>'
        f'<wp:cNvGraphicFramePr><a:graphicFrameLocks xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" noChangeAspect="1"/></wp:cNvGraphicFramePr>'
        f'<a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">'
        f'<a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">'
        f'<pic:pic xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">'
        f'<pic:nvPicPr><pic:cNvPr id="{n}" name="{os.path.basename(path)}"/><pic:cNvPicPr/></pic:nvPicPr>'
        f'<pic:blipFill><a:blip r:embed="{rid}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>'
        f'<pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="{width_emu}" cy="{cy}"/></a:xfrm>'
        f'<a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic>'
        f'</wp:inline></w:drawing></w:r>')
    pic = f'<w:p><w:pPr><w:keepNext/><w:spacing w:before="60" w:after="60"/><w:jc w:val="center"/></w:pPr>{drawing}</w:p>'
    cap = (f'<w:p><w:pPr><w:spacing w:before="0" w:after="120"/><w:jc w:val="center"/></w:pPr>'
           f'{run(caption, False, 18)}</w:p>')
    return pic + cap


def tempo_table(rows):
    widths = (6200, 2304)
    def cell(t, w, bold=False, shade=None, right=False):
        sh = f'<w:shd w:val="clear" w:color="auto" w:fill="{shade}"/>' if shade else ''
        jc = '<w:jc w:val="right"/>' if right else ''
        return (f'<w:tc><w:tcPr><w:tcW w:w="{w}" w:type="dxa"/>{sh}</w:tcPr>'
                f'<w:p><w:pPr><w:spacing w:before="40" w:after="40"/>{jc}</w:pPr>{run(t, bold)}</w:p></w:tc>')
    bd = ''.join(f'<w:{s} w:val="single" w:sz="4" w:space="0" w:color="808080"/>'
                 for s in ("top", "left", "bottom", "right", "insideH", "insideV"))
    out = [f'<w:tbl><w:tblPr><w:tblW w:w="{sum(widths)}" w:type="dxa"/><w:tblBorders>{bd}</w:tblBorders>'
           f'<w:tblLayout w:type="fixed"/><w:tblCellMar><w:left w:w="113" w:type="dxa"/><w:right w:w="113" w:type="dxa"/></w:tblCellMar></w:tblPr>'
           f'<w:tblGrid>' + ''.join(f'<w:gridCol w:w="{w}"/>' for w in widths) + '</w:tblGrid>']
    for i, (a, b) in enumerate(rows):
        head, total = i == 0, i == len(rows) - 1
        shade = 'DDEEEC' if head else ('F2F2F2' if total else None)
        out.append('<w:tr><w:trPr><w:cantSplit/></w:trPr>' + cell(a, widths[0], head or total, shade)
                   + cell(b, widths[1], head or total, shade, right=not head) + '</w:tr>')
    out.append('</w:tbl>')
    return ''.join(out)


# ------------------------------------------------------------------ conteúdo
C = []
C.append(label("A Situação-Problema escolhida:", before=120))
C.append(box([
    "Situação-problema 1 do PIT de Engenharia de Software II: revisar o projeto desenvolvido em Engenharia de "
    "Software I — um aplicativo de vendas para uma loja de cupcakes gourmet — e prepará-lo para a implementação.",
    "A loja já teve uma tentativa malsucedida de vender pela internet, causada por planejamento falho e levantamento "
    "insuficiente de requisitos. Ao revisar o projeto, foram encontradas inconsistências entre os artefatos: "
    "diagramas UML, protótipo e modelo de dados não estavam alinhados entre si nem com os requisitos. Antes de "
    "programar, era preciso corrigir esses pontos, escolher a tecnologia e documentar o banco de dados.",
]))

C.append(label("Objetivo:"))
C.append(box([
    "Revisar e consolidar a documentação do aplicativo da loja de cupcakes para que ela sirva de base confiável ao "
    "desenvolvimento. Para isso, corrigir os diagramas de casos de uso e de classes, ajustar o protótipo das telas, "
    "definir a tecnologia e modelar o banco de dados (modelo lógico, script físico e dicionário de dados), deixando "
    "requisitos, diagramas, protótipo e banco coerentes entre si.",
]))

C.append(label("Revisão de Conceito:"))
C.append(box([
    ('b', "**Engenharia de requisitos:** requisitos funcionais e não funcionais, histórias de usuário e critérios de aceitação."),
    ('b', "**UML — casos de uso:** atores, fronteira do sistema e relações «include» e «extend»."),
    ('b', "**UML — classes:** atributos, métodos, multiplicidade, composição, classe associativa e enumerações."),
    ('b', "**Banco de dados:** modelos conceitual, lógico e físico; chaves primárias e estrangeiras; normalização e restrições de integridade."),
    ('b', "**Dicionário de dados:** descrição de tipo, obrigatoriedade e regra de cada campo."),
    ('b', "**Arquitetura MVC:** separação entre interface (front-end), regras de negócio (back-end) e dados."),
    ('b', "**Prototipação e usabilidade (UX/UI):** fluxo de telas, mensagens de erro e de confirmação."),
    ('b', "**Metodologias ágeis:** backlog priorizado e desenvolvimento incremental."),
]))

C.append(label("Metodologia:"))
C.append(box([
    "Desenvolvimento incremental com práticas ágeis inspiradas no Scrum. O backlog do projeto anterior foi revisado e "
    "priorizado, e a revisão foi feita em ciclos curtos: cada artefato foi conferido contra os requisitos, corrigido e "
    "conferido de novo contra os demais. Para validar o modelo de dados, o script do banco foi executado em um servidor "
    "PostgreSQL e as regras foram testadas com dados válidos e inválidos.",
    "Tecnologias definidas para a implementação:",
    ('b', "**Front-end:** HTML, CSS e JavaScript, com layout responsivo para uso no celular."),
    ('b', "**Back-end:** C# com ASP.NET Core (.NET), organizado no padrão MVC e expondo uma API REST."),
    ('b', "**Banco de dados:** PostgreSQL."),
    ('b', "**Versionamento:** Git e GitHub."),
]))

C.append(label("Tempo:"))
C.append(tempo_table([
    ("Etapa", "Tempo"),
    ("Análise do projeto anterior e levantamento das inconsistências", "1 dia"),
    ("Definição de escopo e da tecnologia", "1 dia"),
    ("Revisão dos diagramas de casos de uso e de classes", "2 dias"),
    ("Modelagem do banco, script físico e dicionário de dados", "2 dias"),
    ("Revisão do protótipo das telas", "1 dia"),
    ("Atualização da documentação", "1 dia"),
    ("Tempo total", "8 dias"),
]))

C.append(label("Procedimento e material utilizado:"))
C.append(box([
    ('n', "Leitura do projeto de Engenharia de Software I e das orientações do PIT II."),
    ('n', "Comparação entre requisitos, diagramas, protótipo e modelo de dados, listando as inconsistências."),
    ('n', "Definição do escopo: categorias de produtos mantidas, cupom de desconto removido e pagamento simulado (na entrega ou na retirada)."),
    ('n', "Escolha da tecnologia: HTML, CSS e JavaScript; ASP.NET Core (C#) no padrão MVC; PostgreSQL."),
    ('n', "Correção do diagrama de casos de uso."),
    ('n', "Correção do diagrama de classes."),
    ('n', "Elaboração do modelo lógico (DER), do script físico e do dicionário de dados."),
    ('n', "Execução do script em PostgreSQL e testes das restrições do banco."),
    ('n', "Revisão do protótipo das telas."),
    ('n', "Atualização da documentação e organização dos arquivos para o repositório no GitHub."),
    "**Materiais utilizados:** Word (documentação), PlantUML e SVG (diagramas), PostgreSQL 16 (banco de dados), "
    "HTML e CSS (protótipo), Git e GitHub (versionamento).",
]))

C.append(label("Resultado e discussão:"))
C.append(box([
    "**Casos de uso:** as ligações entre atores e casos de uso passaram a ser linhas simples, como define a UML. "
    "Saíram “Processar pagamento” (o pagamento é simulado) e “Gerenciar usuários” (sem requisito correspondente). "
    "Entraram “Consultar detalhes do produto”, “Filtrar por categoria” («extend») e “Escolher entrega/retirada e "
    "forma de pagamento” («include»).",
    "**Classes:** foi removida a ligação direta Pedido–Produto, que duplicava a classe associativa ItemPedido, e "
    "incluída a classe Categoria. Os atributos foram alinhados ao banco (preço unitário no item, perfil do usuário, "
    "endereço no pedido, produto ativo e imagem), os valores em dinheiro passaram a usar decimal e foram criadas "
    "enumerações para perfil, status, tipo de entrega e forma de pagamento.",
    "**Banco de dados:** cinco tabelas (usuario, categoria, produto, pedido e item_pedido), com chaves e restrições: "
    "preço e estoque não negativos, quantidade maior que zero, e-mail único, endereço obrigatório para entrega e status "
    "compatível com o tipo de entrega. O subtotal do item é calculado pelo próprio banco. O script foi executado com "
    "sucesso, e os 12 testes com dados inválidos foram todos barrados.",
    "**Protótipo:** o campo de cupom foi removido; os valores do carrinho foram corrigidos (2 × R$ 12,00 = R$ 24,00; "
    "1 × R$ 12,00 = R$ 12,00; total R$ 36,00); foram incluídas as telas de finalização do pedido e de administração; "
    "e as etapas de status do pedido foram padronizadas.",
    "A revisão mostrou que os artefatos estavam desalinhados — o mesmo tipo de falha que levou ao insucesso da primeira "
    "tentativa da loja. Com requisitos, diagramas, protótipo e banco coerentes, o projeto está pronto para a "
    "implementação (Situação-problema 2).",
]))

C.append(label("Diagrama de Caso de Uso:"))
C.append(image("rIdPit1", os.path.join(IMG, "casos-de-uso.png"), CONTENT_EMU,
               "Figura 1 – Diagrama de casos de uso revisado"))
C.append(label("Diagrama de Classes:"))
C.append(image("rIdPit2", os.path.join(IMG, "classes.png"), CONTENT_EMU,
               "Figura 2 – Diagrama de classes revisado"))
C.append(label("Modelo Lógico do Banco de Dados (DER):"))
C.append(image("rIdPit3", os.path.join(IMG, "der.png"), 4_300_000,
               "Figura 3 – Modelo lógico do banco de dados (PostgreSQL)"))
C.append(label("Protótipo do App (Telas Principais):"))
C.append(image("rIdPit4", os.path.join(IMG, "prototipo-telas-1.png"), CONTENT_EMU,
               "Figura 4 – Catálogo com categorias, detalhes do produto e carrinho"))
C.append(image("rIdPit5", os.path.join(IMG, "prototipo-telas-2.png"), CONTENT_EMU,
               "Figura 5 – Finalização do pedido, status do pedido e painel do administrador"))

# ------------------------------------------------------------------ montagem
s = open(DOC, encoding="utf-8").read()
marker = "Situação-problema 3"
i = s.index(marker)
cut = s.index("</w:p>", i) + len("</w:p>")       # fim do parágrafo da 3ª opção
end = s.index("<w:sectPr")                        # propriedades da seção (mantidas)
s = s[:cut] + "".join(C) + s[end:]
open(DOC, "w", encoding="utf-8").write(s)

# imagens novas + relacionamentos; remove as antigas
media = os.path.join(U, "word", "media")
for old in ("image1.jpg", "image2.jpg", "image3.jpg"):
    p = os.path.join(media, old)
    if os.path.exists(p):
        os.remove(p)
rels = open(RELS, encoding="utf-8").read()
rels = re.sub(r'<Relationship Id="rId(8|9|10)"[^>]*media/image[123]\.jpg"/>', "", rels)
new = ""
for n, f in enumerate(["casos-de-uso.png", "classes.png", "der.png",
                       "prototipo-telas-1.png", "prototipo-telas-2.png"], start=1):
    shutil.copy(os.path.join(IMG, f), os.path.join(media, f"pit_{f}"))
    new += (f'<Relationship Id="rIdPit{n}" Type="http://schemas.openxmlformats.org/officeDocument/2006/'
            f'relationships/image" Target="media/pit_{f}"/>')
rels = rels.replace("</Relationships>", new + "</Relationships>")
open(RELS, "w", encoding="utf-8").write(rels)
print("ok")
