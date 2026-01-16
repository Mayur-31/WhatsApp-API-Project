/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./index.html", "./src/**/*.{vue,js}"],
  theme: {
    extend: {
      colors: {
        whatsapp: {
          primary: "#00a884",
          light: "#e4f7f0",
          dark: "#075e54",
          bubble: "#dcf8c6"
        }
      },
    },
  },
  plugins: [],
};