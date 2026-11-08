import type { Metadata, Viewport } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: {
    default: "Almoxarifado & Compras",
    template: "%s · Almoxarifado",
  },
  description: "Controle de estoque, consulta de itens, requisições e pedidos de compra.",
  applicationName: "Almoxarifado",
  manifest: "/manifest.webmanifest",
  icons: {
    icon: "/icon.svg",
    apple: "/icon.svg",
  },
  appleWebApp: {
    capable: true,
    title: "Almoxarifado",
    statusBarStyle: "black-translucent",
  },
  formatDetection: { telephone: false },
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#ffffff" },
    { media: "(prefers-color-scheme: dark)", color: "#0a0a0a" },
  ],
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="pt-BR">
      <body>
        <script
          dangerouslySetInnerHTML={{
            __html: `try{if(localStorage.getItem('tema')==='escuro'){document.documentElement.classList.add('dark')}}catch(e){}`,
          }}
        />
        {children}
      </body>
    </html>
  );
}
