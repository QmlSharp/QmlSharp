cmake_minimum_required(VERSION 4.2 FATAL_ERROR)

option(ENABLE_ASAN "Enable address sanitizer for native targets." OFF)
option(ENABLE_CLANG_TIDY "Enable clang-tidy during native builds." OFF)
option(USE_WERROR "Treat native compiler warnings as errors." OFF)

project(QmlSharp LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_INCLUDE_CURRENT_DIR ON)

# Embed MSVC/UCRT/SDK include paths into compile_commands.json for clangd when using clang-cl.
if(MSVC AND CMAKE_CXX_COMPILER_ID STREQUAL "Clang" AND DEFINED ENV{INCLUDE})
  set(CMAKE_CXX_STANDARD_INCLUDE_DIRECTORIES $ENV{INCLUDE})
  set(CMAKE_C_STANDARD_INCLUDE_DIRECTORIES $ENV{INCLUDE})
endif()

include(cmake/CompileCommands.cmake)
include(cmake/CompilerWarnings.cmake)
include(cmake/Sanitizers.cmake)
include(cmake/StaticAnalysis.cmake)

add_subdirectory(native)

qmlsharp_enable_compile_commands_sync()
