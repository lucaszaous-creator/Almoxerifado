import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Almoxarifado & Compras",
    short_name: "Almoxarifado",
    description: "Controle de estoque, requisições e pedidos de compra.",
    start_url: "/",
    display: "standalone",
    background_color: "#0a0a0a",
    theme_color: "#1f59db",
    lang: "pt-BR",
    icons: [
      { src: "/icon.svg", sizes: "any", type: "image/svg+xml", purpose: "any" },
      { src: "/icon.svg", sizes: "any", type: "image/svg+xml", purpose: "maskable" },
    ],
  };
}
