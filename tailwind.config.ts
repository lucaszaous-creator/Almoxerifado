import type { Config } from "tailwindcss";

const config: Config = {
  darkMode: "class",
  content: ["./src/**/*.{js,ts,jsx,tsx,mdx}"],
  theme: {
    extend: {
      colors: {
        brand: {
          50: "#eef6ff",
          100: "#d9eaff",
          200: "#bcd9ff",
          300: "#8ec1ff",
          400: "#589dff",
          500: "#3178f6",
          600: "#1f59db",
          700: "#1a46b1",
          800: "#1b3d8c",
          900: "#1b3672",
        },
      },
    },
  },
  plugins: [],
};

export default config;
