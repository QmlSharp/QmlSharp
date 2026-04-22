function(qmlsharp_apply_warning_settings target_name)
  if(MSVC)
    set(qmlsharp_warning_flags /W4 /EHsc "$<$<COMPILE_LANGUAGE:CXX>:/permissive->")

    if(CMAKE_CXX_COMPILER_ID STREQUAL "Clang")
      list(
        APPEND
        qmlsharp_warning_flags
        -Wno-unused-command-line-argument
        -Wno-unused-parameter
        -Wno-microsoft-include)
    else()
      list(APPEND qmlsharp_warning_flags /external:anglebrackets /external:W0 /wd4100)
    endif()

    if(USE_WERROR)
      list(APPEND qmlsharp_warning_flags /WX)
    endif()
  elseif(CMAKE_CXX_COMPILER_ID STREQUAL "GNU" OR CMAKE_CXX_COMPILER_ID MATCHES "Clang")
    set(qmlsharp_warning_flags -Wuninitialized -Wall -Wextra -Wpedantic -Wno-unused-parameter -Wshadow)

    if(USE_WERROR)
      list(APPEND qmlsharp_warning_flags -Werror)
    endif()
  else()
    set(qmlsharp_warning_flags)
  endif()

  if(qmlsharp_warning_flags)
    target_compile_options(${target_name} PRIVATE ${qmlsharp_warning_flags})
  endif()
endfunction()
