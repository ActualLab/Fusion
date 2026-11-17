import { createContentLoader } from "vitepress";

export interface Post {
  title: string;
  url: string;
  date: string;
  year: string;
}

declare const data: Post[];
export { data };

// Every posts/YYMMDD-title.md is a post: the date comes from the file name, the title from
// the "title" frontmatter field or the first heading. Newest first.
export default createContentLoader("posts/*.md", {
  includeSrc: true,
  transform(pages): Post[] {
    const posts: Post[] = [];
    for (const page of pages) {
      const m = /\/(\d{2})(\d{2})(\d{2})-[^/]*$/.exec(page.url);
      if (!m)
        continue;
      const title = page.frontmatter.title ?? /^#\s+(.+)$/m.exec(page.src ?? "")?.[1] ?? page.url;
      posts.push({ title, url: page.url, date: `20${m[1]}-${m[2]}-${m[3]}`, year: `20${m[1]}` });
    }
    return posts.sort((a, b) => b.date.localeCompare(a.date));
  },
});
