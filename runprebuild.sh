#!/bin/sh

case "$1" in

  'clean')

    mono Prebuild/Prebuild.exe /clean

  ;;


  'autoclean')

    echo y|mono Prebuild/Prebuild.exe /clean

  ;;


  'vs2010')
  
    mono Prebuild/Prebuild.exe /target vs2013
  
  ;;

  *)

    mono Prebuild/Prebuild.exe /target vs2013

  ;;

esac
