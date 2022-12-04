# Overview
Some tools for code editing.

# Features

- Unify file's line feed
- Unify file's character encoding
- Others
  - Load settings from a file. Able to use an individual setting for each projects.

## Settings

![](./doc/Settings.jpg)

| Category        | Item               | Description                                     | Default       |
| :---            | :----------------- | :---------------------------------------------- | :------------ |
| General         |                    |                                                 |               |
|                 | Load Setting Files | Load settings from a file                       | true          |
| Unify Encoding  |                    |                                                 |               |
|                 | Encoding           | Character encoding for unifying                 | UTF8          |
| Unify Line Feed |                    |                                                 |               |
|                 | C/C++              | Line feed for C/C++                             | LF            |
|                 | CSharp             | Line feed for C#                                | LF            |
|                 | Others             | Line feed for other text format                 | LF            |

### Setting File
Try to find a setting file,
1. From the directory of the focused file
2. Search **_handytools.xml**
3. If the current directory don't have ".git" or ".svn", move to upper directory and repeat from 2.

An example setting file "_handytools.xml" is below,

```
<?xml version="1.0" encoding="utf-8"?>
<HandyTools>
    <General>
        <Indexing>false</Indexing>
    </General>
    <UnifyLineFeed>
        <Code lang="C/C++">
            LF
        </Code>
        <Code lang="CSharp">
            CRLF
        </Code>
        <Code lang="Others">
            LF
        </Code>
    </UnifyLineFeed>
    <UnifyEncoding>
        <Encoding>UTF8</Encoding>
    </UnifyEncoding>
</HandyTools>
```

# ToDo

- Merge full indexing search

# License
This software is distributed under two licenses 'The MIT License' or 'Public Domain', choose whichever you like.

