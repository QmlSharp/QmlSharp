function(qmlsharp_find_qt)
  if(DEFINED ENV{QT_DIR} AND NOT "$ENV{QT_DIR}" STREQUAL "")
    list(PREPEND CMAKE_PREFIX_PATH "$ENV{QT_DIR}")
  endif()

  find_package(Qt6 REQUIRED COMPONENTS Core Gui Qml Quick)
endfunction()
