# Posts

<script setup>
import { data as posts } from "./posts.data.mts";

const recent = posts.slice(0, 5);
const years = [...new Set(posts.map(p => p.year))];
</script>

## Recent

<ul>
  <li v-for="post in recent" :key="post.url">
    {{ post.date }}: <a :href="post.url">{{ post.title }}</a>
  </li>
</ul>

<template v-for="year in years" :key="year">
  <h2 :id="`posts-${year}`">{{ year }}</h2>
  <ul>
    <li v-for="post in posts.filter(p => p.year === year)" :key="post.url">
      {{ post.date }}: <a :href="post.url">{{ post.title }}</a>
    </li>
  </ul>
</template>

## Elsewhere

Blog posts and articles about Fusion published on other sites:

- [Popular UI architectures compared & how Blazor+Fusion UI fits in there](https://itnext.io/the-most-popular-ui-architectures-and-how-fusion-based-ui-fits-in-there-fb47e45038a7?source=friends_link&sk=bef676ccd1f3c6e6148178bf05346852)
- [The Ungreen Web: Why our web apps are terribly inefficient?](https://alexyakunin.medium.com/the-ungreen-web-why-our-web-apps-are-terribly-inefficient-28791ed48035?source=friends_link&sk=74fb46086ca13ff4fea387d6245cb52b)
- [Why real-time UI is inevitable future for web apps?](https://medium.com/@alexyakunin/features-of-the-future-web-apps-part-1-e32cf4e4e4f4?source=friends_link&sk=65dacdbf670ef9b5d961c4c666e223e2)
- [How similar is Fusion to SignalR?](https://medium.com/@alexyakunin/how-similar-is-stl-fusion-to-signalr-e751c14b70c3?source=friends_link&sk=241d5293494e352f3db338d93c352249)
- [How similar is Fusion to Knockout / MobX?](https://medium.com/@alexyakunin/how-similar-is-stl-fusion-to-knockout-mobx-fcebd0bef5d5?source=friends_link&sk=a808f7c46c4d5613605f8ada732e790e)
- [Fusion In Simple Terms](https://medium.com/@alexyakunin/stl-fusion-in-simple-terms-65b1975967ab?source=friends_link&sk=04e73e75a52768cf7c3330744a9b1e38)
