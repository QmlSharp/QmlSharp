option(QMLSHARP_ENABLE_SANITIZERS "Enable native sanitizer instrumentation." OFF)

function(qmlsharp_enable_sanitizers target_name)
  if(NOT QMLSHARP_ENABLE_SANITIZERS)
    return()
  endif()

  if(CMAKE_CXX_COMPILER_ID MATCHES "Clang|GNU")
    target_compile_options(${target_name} PRIVATE -fsanitize=address,undefined)
    target_link_options(${target_name} PRIVATE -fsanitize=address,undefined)
  endif()
endfunction()
