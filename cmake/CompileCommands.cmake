function(qmlsharp_enable_compile_commands_sync)
  if(NOT CMAKE_EXPORT_COMPILE_COMMANDS)
    return()
  endif()

  add_custom_target(
    sync-compile-commands ALL
    COMMAND ${CMAKE_COMMAND} -E make_directory "${CMAKE_SOURCE_DIR}/build"
    COMMAND
      ${CMAKE_COMMAND} -E copy_if_different "${CMAKE_BINARY_DIR}/compile_commands.json"
      "${CMAKE_SOURCE_DIR}/build/compile_commands.json"
    COMMENT "Syncing compile_commands.json -> build/"
    VERBATIM)
endfunction()
