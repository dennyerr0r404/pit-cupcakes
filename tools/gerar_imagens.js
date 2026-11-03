// Gera as ilustrações SVG dos cupcakes (mesmo desenho do protótipo) em wwwroot/img
const fs = require("fs"), path = require("path");
const destino = path.resolve(__dirname, "../src/Cupcakes.Api/wwwroot/img");
fs.mkdirSync(destino, { recursive: true });

function cup(massa, cobertura, granulado, topo = "#C93C4A") {
  const g = granulado ? [[34,26],[50,20],[62,30],[44,36],[28,38],[58,40]].map(([x,y],i) =>
    `<rect x="${x}" y="${y}" width="6" height="2.6" rx="1.3" fill="${granulado[i % granulado.length]}" transform="rotate(${i*37} ${x} ${y})"/>`).join("") : "";
  const listras = [30,38,46,54,62,70].map(x => `<line x1="${x}" y1="55" x2="${(x-(x-48)*0.2).toFixed(1)}" y2="87" stroke="rgba(0,0,0,.18)" stroke-width="2"/>`).join("");
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96" width="96" height="96">
<path d="M22 54 L74 54 L66 88 L30 88 Z" fill="${massa}"/>${listras}
<ellipse cx="48" cy="52" rx="30" ry="9" fill="${cobertura}"/>
<path d="M20 52 Q18 36 34 34 Q34 18 50 16 Q66 16 64 32 Q80 34 76 52 Z" fill="${cobertura}"/>
<path d="M30 40 Q40 46 52 40 Q62 34 70 42" stroke="rgba(0,0,0,.08)" stroke-width="3" fill="none"/>
<circle cx="50" cy="13" r="5" fill="${topo}"/>${g}
</svg>\n`;
}

const sabores = {
  "velvet-red":       ["#A3242F", "#FBF3E6", ["#D9434F"]],
  "chocolate-belga":  ["#4A2C22", "#6B3A26", ["#F3E1C8"]],
  "baunilha":         ["#E9C98F", "#FFF1F4", ["#E85D8C", "#4BA3D9", "#F2C230"]],
  "cenoura-vegana":   ["#D9822B", "#5A3A2A", ["#F2A65A"]],
  "limao-vegano":     ["#F2DE7A", "#FFFBEA", ["#9BC53D"], "#9BC53D"],
  "pistache":         ["#E9C98F", "#B9CF8A", ["#7C9A45"], "#7C9A45"],
  "frutas-vermelhas": ["#E9C98F", "#F2B8C6", ["#B0213A", "#5B2A86"]],
  "cupcake-padrao":   ["#E9C98F", "#F6D6DC", ["#2F8F8B"]],
};
for (const [nome, args] of Object.entries(sabores)) fs.writeFileSync(path.join(destino, nome + ".svg"), cup(...args));
// Ícone da aba do navegador
fs.writeFileSync(path.join(destino, "favicon.svg"), cup("#2F8F8B", "#F9E1E3", ["#C93C4A"]));
console.log("imagens geradas em", destino);
