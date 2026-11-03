"""Gera o diagrama de casos de uso (SVG) do sistema de cupcakes."""
import math, sys

W, H = 1200, 900
TEAL, FILL, INK = "#3D7D7A", "#FFF7F2", "#222"

ucs = {  # id: (cx, cy, rx, texto)
    "cad":  (420, 140, 115, ["Cadastrar-se"]),
    "cat":  (420, 225, 115, ["Visualizar catálogo"]),
    "det":  (420, 310, 115, ["Consultar detalhes", "do produto"]),
    "car":  (420, 395, 115, ["Gerenciar carrinho"]),
    "fin":  (420, 480, 115, ["Finalizar pedido"]),
    "sta":  (420, 565, 115, ["Acompanhar status", "do pedido"]),
    "fil":  (790, 225, 115, ["Filtrar por categoria"]),
    "ent":  (790, 480, 125, ["Escolher entrega/retirada", "e forma de pagamento"]),
    "log":  (420, 655, 105, ["Fazer login"]),
    "prod": (790, 745, 115, ["Gerenciar produtos"]),
    "ped":  (790, 825, 115, ["Gerenciar pedidos", "(atualizar status)"]),
}
RY = 32

def edge(uc, tx, ty):
    """Ponto na borda da elipse na direção de (tx, ty)."""
    cx, cy, rx, _ = ucs[uc]
    a = math.atan2(ty - cy, tx - cx)
    t = math.atan2(rx * math.sin(a), RY * math.cos(a))
    return cx + rx * math.cos(t), cy + RY * math.sin(t)

def actor(x, y, nome):
    return f'''<g stroke="{INK}" stroke-width="2.2" fill="none">
 <circle cx="{x}" cy="{y-48}" r="16" fill="#fff"/>
 <line x1="{x}" y1="{y-32}" x2="{x}" y2="{y+10}"/>
 <line x1="{x-26}" y1="{y-18}" x2="{x+26}" y2="{y-18}"/>
 <line x1="{x}" y1="{y+10}" x2="{x-22}" y2="{y+44}"/>
 <line x1="{x}" y1="{y+10}" x2="{x+22}" y2="{y+44}"/></g>
<text x="{x}" y="{y+70}" text-anchor="middle" font-size="18" fill="{INK}">{nome}</text>'''

out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="Arial, Helvetica, sans-serif">',
       '<defs><marker id="seta" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="10" markerHeight="10" orient="auto-start-reverse">'
       f'<path d="M0,0 L10,5 L0,10" fill="none" stroke="{INK}" stroke-width="1.6"/></marker></defs>',
       f'<rect width="{W}" height="{H}" fill="#fff"/>',
       f'<rect x="260" y="70" width="700" height="805" fill="none" stroke="{TEAL}" stroke-width="2.5"/>',
       f'<text x="610" y="105" text-anchor="middle" font-size="21" font-weight="bold" fill="{INK}">Sistema de Vendas de Cupcakes</text>']

cli, adm = (110, 420), (1090, 760)
for uc in ["cad", "cat", "det", "car", "fin", "sta", "log"]:
    x, y = ucs[uc][0] - ucs[uc][2], ucs[uc][1]  # ponta esquerda da elipse
    out.append(f'<line x1="{cli[0]+30}" y1="{cli[1]-14}" x2="{x:.1f}" y2="{y:.1f}" stroke="{INK}" stroke-width="1.6"/>')
for uc in ["log", "prod", "ped"]:
    x, y = ucs[uc][0] + ucs[uc][2], ucs[uc][1]  # ponta direita da elipse
    out.append(f'<line x1="{adm[0]-30}" y1="{adm[1]-14}" x2="{x:.1f}" y2="{y:.1f}" stroke="{INK}" stroke-width="1.6"/>')

def dep(a, b, rotulo):
    (ax, ay), (bx, by) = edge(a, *ucs[b][:2]), edge(b, *ucs[a][:2])
    out.append(f'<line x1="{ax:.1f}" y1="{ay:.1f}" x2="{bx:.1f}" y2="{by:.1f}" stroke="{INK}" stroke-width="1.6" stroke-dasharray="8 6" marker-end="url(#seta)"/>')
    out.append(f'<text x="{(ax+bx)/2:.0f}" y="{(ay+by)/2-10:.0f}" text-anchor="middle" font-size="16" fill="{INK}">«{rotulo}»</text>')

dep("fil", "cat", "extend")   # Filtrar estende Visualizar catálogo
dep("fin", "ent", "include")  # Finalizar inclui Escolher entrega/pagamento

for k, (cx, cy, rx, linhas) in ucs.items():
    out.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{RY}" fill="{FILL}" stroke="{TEAL}" stroke-width="2"/>')
    y0 = cy + 6 - (len(linhas) - 1) * 10
    for i, l in enumerate(linhas):
        out.append(f'<text x="{cx}" y="{y0 + i*20}" text-anchor="middle" font-size="17" fill="{INK}">{l}</text>')

out += [actor(*cli, "Cliente"), actor(*adm, "Administrador"), "</svg>"]
open(sys.argv[1], "w", encoding="utf-8").write("\n".join(out))
