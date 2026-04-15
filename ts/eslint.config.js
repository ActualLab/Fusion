import tseslint from "typescript-eslint";
import tsParser from "@typescript-eslint/parser";
import prettier from "eslint-plugin-prettier";

export default tseslint.config(
  {
    ignores: ["**/dist/"],
  },
  ...tseslint.configs.recommendedTypeChecked,
  ...tseslint.configs.strictTypeChecked,
  ...tseslint.configs.stylisticTypeChecked,
  {
    rules: {
      indent: ["error", 4],
      quotes: ["error", "single", {
        allowTemplateLiterals: true,
        avoidEscape: true,
      }],
      "object-curly-spacing": ["error", "always"],
      "@typescript-eslint/no-extraneous-class": ["error", {
        allowStaticOnly: true,
      }],
      "@typescript-eslint/restrict-template-expressions": ["error", {
        allowNumber: true,
        allowBoolean: true,
        allowNullish: true,
      }],
      "@typescript-eslint/no-non-null-assertion": "off",
      "@typescript-eslint/no-confusing-void-expression": ["error", {
        ignoreArrowShorthand: true,
      }],
      "@typescript-eslint/dot-notation": "off",
      "@typescript-eslint/no-unused-vars": [
        "error",
        {
          caughtErrors: "all",
          vars: "all",
          argsIgnorePattern: "^_",
          varsIgnorePattern: "^_",
          caughtErrorsIgnorePattern: "^_",
          destructuredArrayIgnorePattern: "^_",
          ignoreRestSiblings: true,
        },
      ],
    },
    languageOptions: {
      parser: tsParser,
      ecmaVersion: 2020,
      sourceType: "module",
      parserOptions: {
        tsconfigRootDir: import.meta.dirname,
        projectService: true,
        allowDefaultProject: ["*.ts"],
      },
    },
    plugins: {
      "@typescript-eslint": tseslint.plugin,
      prettier,
    },
  },
);
