function(qmlsharp_configure_static_analysis target_name)
  if(NOT ENABLE_CLANG_TIDY)
    return()
  endif()

  find_program(CLANG_TIDY_EXECUTABLE NAMES clang-tidy)
  if(NOT CLANG_TIDY_EXECUTABLE)
    message(WARNING "ENABLE_CLANG_TIDY is ON but clang-tidy was not found in PATH.")
    return()
  endif()

  set_target_properties(${target_name} PROPERTIES CXX_CLANG_TIDY "${CLANG_TIDY_EXECUTABLE};--quiet")
endfunction()
