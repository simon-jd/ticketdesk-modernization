# Search results: legacy (Lucene, golden) vs new app (SQL Server full-text)

| Exchange | Request | Legacy results | New results | In both | Same rank | Legacy ids | New ids |
|---|---|---:|---:|---:|---:|---|---|
| `search#002.0` | `GET /search` | 0 | 0 | 0 | 0 |  |  |
| `search#003.0` | `GET /search?term=printer` | 20 | 20 | 18 | 0 | 2789 2992 3994 4596 6817 7081 9284 9703 813 3857 3979 3989 5255 76 2364 3582 4041 4162 4931 6161 | 76 813 1546 2364 2789 2992 3582 3857 3979 3989 3994 4041 4162 4596 4931 5255 6161 6817 7081 7980 |
| `search#004.0` | `GET /search/network` | 20 | 20 | 12 | 0 | 1633 3318 7405 9899 326 627 1864 2913 3304 7718 9267 9409 3816 4786 5218 1086 6334 8055 3907 8924 | 742 2248 8499 180 5633 9330 326 627 1086 1633 1864 2913 3304 3318 3668 3816 3907 4744 4786 5218 |
| `search#005.0` | `GET /search?term=zzqqxxnohits` | 0 | 0 | 0 | 0 |  |  |
