import DefaultTheme from "vitepress/theme";
import BarChart from "./BarChart.vue";
import AnimatedSvg from "./AnimatedSvg.vue";
import "./custom.css";
import "./mermaid.css";

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component("BarChart", BarChart);
    app.component("AnimatedSvg", AnimatedSvg);
  },
};
