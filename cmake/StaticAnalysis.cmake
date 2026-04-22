option(QMLSHARP_ENABLE_CLANG_TIDY "Enable clang-tidy for native targets." OFF)

function(qmlsharp_configure_static_analysis target_name)
  if(NOT QMLSHARP_ENABLE_CLANG_TIDY)
    return()
  endif()

  find_program(CLANG_TIDY_EXECUTABLE NAMES clang-tidy)
  if(CLANG_TIDY_EXECUTABLE)
    set_target_properties(${target_name} PROPERTIES CXX_CLANG_TIDY "${CLANG_TIDY_EXECUTABLE}")
  endif()
endfunction()
