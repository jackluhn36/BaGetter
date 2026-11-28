import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# Search

BaGest implements NuGet's [search query service](https://learn.microsoft.com/nuget/api/search-query-service-resource)
at `/v3/search` and its [autocomplete service](https://learn.microsoft.com/nuget/api/search-autocomplete-service-resource)
at `/v3/autocomplete`. NuGet clients, Visual Studio and BaGest's own UI use them.

## Matching

The search query is split on spaces into keywords, and a package matches when it contains **every** keyword.
Each keyword can appear anywhere in the package ID, description, summary or title, and case is ignored:
`json SERIALIZER` finds a package whose ID contains `Json` and whose description contains `serializer`.
Autocomplete matches the package ID only.

Repeated keywords count once, and only the first 10 different keywords are used.

:::note

With SQLite, case is ignored for ASCII letters only, because SQLite's built-in
[`lower()`](https://www.sqlite.org/lang_corefunc.html#lower) does not convert other characters.
SQL Server and MySQL ignore case for all letters, and so does PostgreSQL unless the database uses the `C` locale.

:::

## Sort order

The NuGet search protocol does not define an order for the results, and NuGet clients do not ask for one.
As an extension, BaGest's `/v3/search` accepts a `sortBy` parameter with the same values as nuget.org's
gallery search, the service behind the "Sort by" menu on [nuget.org](https://www.nuget.org/packages):

| `sortBy` | Order |
| --- | --- |
| `relevance` | The default order. BaGest does not score results: pages are chosen in package ID order. |
| `totalDownloads-desc` | Most downloads first, counting the versions the search returns, as in its `totalDownloads`. |
| `totalDownloads-asc` | Fewest downloads first. |
| `published` | Most recently published first, by the newest version of each package. |
| `created-desc` | The same as `published`. |
| `created-asc` | Least recently published first, by the newest version of each package. |
| `lastEdited` | The same as `published`: BaGest does not record metadata edits. |
| `title-asc` | By the title of the most recently published version, ignoring case. The package ID is used for packages without a title. |
| `title-desc` | By title in reverse, ignoring case. |

As on nuget.org, values are not case-sensitive, and a missing or unknown value means `relevance`, so a request
that works on nuget.org also works on BaGest. With the other values, packages that are equal under the chosen
order are ordered by ID, so `skip` and `take` page through the results without gaps or repeats.

<Tabs>
  <TabItem value="curl" label="curl" default>

```shell
curl "http://localhost:5000/v3/search?q=json&sortBy=totalDownloads-desc&take=10"
```

  </TabItem>
  <TabItem value="powershell" label="PowerShell">

```powershell
Invoke-RestMethod "http://localhost:5000/v3/search?q=json&sortBy=totalDownloads-desc&take=10"
```

  </TabItem>
</Tabs>

The search page in BaGest's UI offers the same orders as nuget.org: "Relevance", "Downloads"
(`totalDownloads-desc`) and "Recently updated" (`created-desc`). The other values work in links, for example
`/?q=json&sortBy=title-asc`.

Sorting is supported when search uses the database, which is the default. Azure Table Storage search ignores
`sortBy` and returns results in its usual order.
