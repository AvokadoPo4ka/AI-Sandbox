# AI Sandbox

**AI-powered interactive sandbox built with Unity and a local Large Language Model (LLM).**

AI Sandbox is an experimental Unity project exploring how artificial intelligence can interact with a game world through natural language commands.

The project connects a local language model to a Unity runtime, allowing users to describe actions in natural language and have the AI generate structured instructions that can be processed by the game.

## Features

- **Natural Language Interaction** — interact with the game using text commands.
- **Local AI Integration** — communicate with a locally hosted language model through the Ollama API.
- **Structured Responses** — process AI-generated JSON plans instead of directly executing arbitrary model output.
- **Runtime Object Creation** — create objects in the game world through the AI execution system.
- **Modular Architecture** — separate request handling, AI communication, validation, and runtime execution.
- **Request Management** — support request cancellation and lifecycle management.

## Architecture

The project is organized around several main components:

| Component | Responsibility |
|---|---|
| `SendRequest` | Handles user input, communicates with the AI provider, and manages requests. |
| `CreationPrompt` | Defines the instructions and expected response format for the AI. |
| `CreationRuntime` | Validates and executes supported creation plans inside Unity. |
| `ActionRegistry` | Provides a registry for available runtime actions. |
| `OllamaProvider` | Handles communication with the local Ollama API. |

The AI does not directly control Unity objects. Instead, it produces structured instructions that are parsed, validated, and passed to the runtime execution system.

## Technology Stack

- **Unity:** 2021.3.45f2
- **Language:** C#
- **AI Runtime:** Ollama
- **AI Model:** Qwen 2.5 7B (previously used; the active model depends on the local configuration)
- **Platform:** Windows Desktop
- **Scripting Backend:** Mono

## Requirements

To run the project, you need:

- Unity 2021.3.45f2 or a compatible Unity 2021.3 LTS installation.
- Ollama installed and running locally.
- A compatible language model downloaded in Ollama.
- A Windows PC capable of running Unity and the selected local model.

## Getting Started

1. Clone or download this repository.
2. Open the project using Unity Hub.
3. Install and launch Ollama.
4. Download a compatible model(ollama pull qwen2.5:7b).
5. Make sure the Ollama API is available at the endpoint configured in the project.
6. Open the main game scene and enter Play Mode(or build the game, if you want to).
7. Use the in-game AI interface to submit a natural language command.


## Project Status

AI Sandbox is under active development.

The current development focuses on:

- Reliable AI request handling.
- Runtime creation lifecycle management.
- Safe validation and execution of AI-generated instructions.
- Better awareness of the game world.
- Extensible gameplay capabilities.

Some planned features are not yet implemented and should not be considered available in the current build.

## Safety and Execution

AI-generated responses are treated as untrusted input.

The project uses structured data, validation, and registered runtime functionality to control which operations can be executed.

The long-term goal is to expand the system while maintaining clear boundaries between AI-generated instructions and actual Unity operations.