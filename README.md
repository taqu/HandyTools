# Overview
Some tools for code editing.

# Features

- Unify file's line feed
- Unify file's character encoding
- Editing command with LLM
  - Support OpenAI API and Ollama API
  - Support local API endpoint
- Others
  - Load settings from a file. Able to use an individual setting for each projects.

## AI Features
Just right click on the target code, and select command from Handy Tools submenu.
![](./doc/AIMenu.jpg)

| Command          | Place                 | Description                                                        |
| :------          | :----                 | :----------                                                        |
| Complete         | Selection or one line | Complete after the selection or one line if nothing selected.      |
| Explain Function | Selection or one line | Explain a funciton which the selection or one line is overlapping. |
| Translate        | Selection             | Translate the selected text on the chat view.                      |
| Add Doxygen      | Selection or one line | Add Doxygen style document for a function,                         |
|                  |                       | which the selection or one line is overlapping.                    |
| Preview Doxygen  | Selection of one line | Preview Doxygen style document for a function on the chat view.    |

![](./doc/HandyTools00.gif)

*Notice: now supports only C/C++*

## Settings

### General
![](./doc/Settings.jpg)

| Category        | Item               | Description                     | Default |
| :-------        | :---               | :----------                     | :------ |
| General         |                    |                                 |         |
|                 | Load Setting Files | Load settings from a file       | true    |
| Unify Encoding  |                    |                                 |         |
|                 | Encoding           | Character encoding for unifying | UTF8    |
| Unify Line Feed |                    |                                 |         |
|                 | C/C++              | Line feed for C/C++             | LF      |
|                 | CSharp             | Line feed for C#                | LF      |
|                 | Others             | Line feed for other text format | LF      |

### AI
![](./doc/SettingsAI.jpg)

| AI     | Item                    | Description                                                     | Default |
| :---   | :---                    | :----------                                                     | :------ |
| API    | API Endpoint            | Endpoint Address                                                | empty   |
|        | API Key                 | API Key                                                         | XXX     |
|        | API Type                | OpenAI or Ollama                                                | OpenAI  |
| Model  | Format After Generation | Whether format text after generation                            | false   |
|        | Completion Model Name   | Model for completion tasks                                      | llama2  |
|        | Max Text Length         | Max text length for passing to LLM (in chars, not context size) | llama2  |
|        | Model Name              | Model for general tasks                                         | llama2  |
|        | Temperature             | Temperature in generation parameters                            | 0.1     |
|        | Translation Model Name  | Model for translation tasks                                     | llama2  |
| Prompt | Completion              | Prompt for completion tasks                                    |         |
|        | Documentation           | Prompt for documentation tasks                                 |         |
|        | Explanation             | Prompt for explanation tasks                                   |         |
|        | Translation             | Prompt for translation tasks                                   |         |

### Setting File
Try to find a setting file,
1. From the directory of the focused file
2. Search **_handytools.xml**
3. If the current directory don't have ".git" or ".svn", move to upper directory and repeat from 2.

#### General Settings
An example general setting file "_handytools.xml" is below,

```xml
<?xml version="1.0" encoding="utf-8"?>
<HandyTools>
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

#### General Settings
An example settings for [Ollama](https://ollama.com/) endpoint,

```xml
<?xml version="1.0" encoding="utf-8"?>
<HandyTools>
	<AI>
		<!-- valid values: OpenAI or Ollama -->
		<APIType>Ollama</APIType>
		<ModelGeneral>llama2</ModelGeneral>
		<ModelGeneration>llama2</ModelGeneration>
		<ModelTranslation>llama2</ModelTranslation>
		<ApiKey>XXX</ApiKey>
		<ApiEndpoint>http://localhost:11434</ApiEndpoint>
		<PromptCompletion>Complete the next {filetype} code. Write only the code, not the explanation.\ncode:{content}</PromptCompletion>
		<PromptExplanation>Explain the next {filetype} code.\ncode:{content}</PromptExplanation>
		<PromptTranslation>Translate in English\n\n{content}</PromptTranslation>
		<PromptDocumentation>Create a doxygen comment for the following C++ Function. doxygen comment only\n\n{content}</PromptDocumentation>
		<Temperature>0.1</Temperature>
        <MaxTextLength>4096</MaxTextLength>
	</AI>
</HandyTools>
```

```xml
<?xml version="1.0" encoding="utf-8"?>
<HandyTools>
	<AI>
		<!-- valid values: OpenAI or Ollama -->
		<APIType>OpenAI</APIType>
		<ModelGeneral>gpt3.5-turbo</ModelGeneral>
		<ModelGeneration>gpt3.5-turbo</ModelGeneration>
		<ModelTranslation>gpt3.5-turbo</ModelTranslation>
		<ApiKey>XXX</ApiKey>
		<ApiEndpoint></ApiEndpoint>
	</AI>
</HandyTools>
```
# ToDo

# License
This software is distributed under two licenses 'The MIT License' or 'Public Domain', choose whichever you like.

