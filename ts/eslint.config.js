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
      // Relaxed rules for the RPC/core packages (low-level wire protocol code)
      "@typescript-eslint/no-explicit-any": "off",
      "@typescript-eslint/no-unnecessary-condition": "off",
      "@typescript-eslint/no-unnecessary-type-assertion": "off",
      "@typescript-eslint/no-unsafe-assignment": "off",
      "@typescript-eslint/no-unsafe-member-access": "off",
      "@typescript-eslint/no-unsafe-call": "off",
      "@typescript-eslint/no-unsafe-argument": "off",
      "@typescript-eslint/no-unsafe-return": "off",
      "@typescript-eslint/no-unsafe-function-type": "off",
      "@typescript-eslint/no-unnecessary-type-parameters": "off",
      "@typescript-eslint/no-this-alias": "off",
      "@typescript-eslint/array-type": "off",
      "@typescript-eslint/require-await": "off",
      "@typescript-eslint/no-empty-function": "off",
      "@typescript-eslint/consistent-generic-constructors": "off",
      "@typescript-eslint/class-literal-property-style": "off",
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
